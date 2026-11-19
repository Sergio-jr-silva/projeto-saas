using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Nexo.Agent.Core.Connectors;
using Nexo.Agent.Core.Queue;
using Nexo.Agent.Core.Security;
using Nexo.Agent.Core.State;
using Nexo.Agent.Core.Sync;

namespace Nexo.Agent.Desktop;

public class MainForm : Form
{
    private IConfiguration _config;
    private AgentStateManager _stateManager;
    private PersistentTaskQueue _taskQueue;
    private readonly AgentCredentialsManager _credsManager;
    private IDatabaseConnector _connector = null!;
    private IncrementalSyncEngine _syncEngine = null!;

    private readonly System.Windows.Forms.Timer _stateRefreshTimer;
    private readonly System.Windows.Forms.Timer _backgroundSyncTimer;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;

    // Tray Menu Items
    private ToolStripMenuItem _menuStatus = null!;
    private ToolStripMenuItem _menuEmpresa = null!;
    private ToolStripMenuItem _menuFilial = null!;
    private ToolStripMenuItem _menuBanco = null!;
    private ToolStripMenuItem _menuUltimaSync = null!;

    // UI Status Labels
    private Label _lblEmpresaNome = null!;
    private Label _lblFilialNome = null!;
    private Label _lblStatusInternet = null!;
    private Label _lblStatusBanco = null!;
    private Label _lblStatusSync = null!;
    private Label _lblCargaInicial = null!;
    private Label _lblUltimaSync = null!;

    private RichTextBox _rtbLog = null!;
    private Button _btnSincronizar = null!;
    private Button _btnConfig = null!;
    private Button _btnMinimizar = null!;
    private CheckBox _chkRunInBackground = null!;
    private Label _lblFooterStatus = null!;

    private bool _isRealExit = false;

    public MainForm(IConfiguration config)
    {
        _config = config;

        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

        _credsManager = new AgentCredentialsManager(dataDir);
        _stateManager = new AgentStateManager(Path.Combine(dataDir, "agent_state.json"));
        _taskQueue = new PersistentTaskQueue(Path.Combine(dataDir, "offline_queue.json"));

        InitializeEngine();

        // Configuração de ícone da janela
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
                this.Icon = new Icon(iconPath);
            else
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            this.Icon = SystemIcons.Application;
        }

        // Form settings
        Text = "Nexo Compras Agent (SINc) — Sincronizador Local ERP";
        Size = new Size(880, 680);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // System Tray Menu & NotifyIcon
        _trayMenu = new ContextMenuStrip();
        BuildTrayMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = this.Icon,
            Text = "Nexo Compras Agent",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (s, e) => ShowAndRestore();

        InitializeUi();

        // Timer de atualização da interface (a cada 2 segundos)
        _stateRefreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _stateRefreshTimer.Tick += (s, e) => AtualizarDadosVisualizacao();
        _stateRefreshTimer.Start();

        // Timer de ciclo em segundo plano (respeita intervalo configurado)
        var heartbeatSec = int.TryParse(_config["Sync:IntervaloHeartbeatSeg"], out var hs) ? hs : 60;
        _backgroundSyncTimer = new System.Windows.Forms.Timer { Interval = Math.Max(10000, heartbeatSec * 1000) };
        _backgroundSyncTimer.Tick += async (s, e) => await ExecutarCicloSegundoPlano();
        _backgroundSyncTimer.Start();

        Shown += async (s, e) =>
        {
            LogToUi("INFO", "Nexo Compras Agent (SINc) iniciado silenciosamente.");
            await ExecutarTesteConectividade();
            AtualizarDadosVisualizacao();
        };

        FormClosing += OnFormClosingHandler;
    }

    private void InitializeEngine()
    {
        _connector = ConnectorFactory.CreateConnector(_config);
        _syncEngine = new IncrementalSyncEngine(_connector, _config, _taskQueue, _stateManager);

        _syncEngine.OnLog += (lvl, msg) =>
        {
            if (InvokeRequired) Invoke(() => LogToUi(lvl, msg));
            else LogToUi(lvl, msg);
        };
    }

    private void BuildTrayMenu()
    {
        _trayMenu.Items.Clear();

        _menuStatus = new ToolStripMenuItem("Status: Carregando...") { Enabled = false, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _menuEmpresa = new ToolStripMenuItem("Empresa: Aguardando vinculação...") { Enabled = false };
        _menuFilial = new ToolStripMenuItem("Filial: Matriz") { Enabled = false };
        _menuBanco = new ToolStripMenuItem("Banco: Inicializando...") { Enabled = false };
        _menuUltimaSync = new ToolStripMenuItem("Última Sincronização: --") { Enabled = false };

        var sep1 = new ToolStripSeparator();
        var menuSyncNow = new ToolStripMenuItem("🔄 Sincronizar Agora", null, async (s, e) => await ExecutarSincronizacaoTotal())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        var menuConfig = new ToolStripMenuItem("⚙️ Configurações...", null, (s, e) => AbrirTelaConfiguracoes());
        var menuAbrir = new ToolStripMenuItem("🖥️ Abrir Interface", null, (s, e) => ShowAndRestore());

        var sep2 = new ToolStripSeparator();
        var menuSair = new ToolStripMenuItem("❌ Encerrar Agente", null, (s, e) => FecharInterface());

        _trayMenu.Items.AddRange(new ToolStripItem[]
        {
            _menuStatus,
            _menuEmpresa,
            _menuFilial,
            _menuBanco,
            _menuUltimaSync,
            sep1,
            menuSyncNow,
            menuAbrir,
            menuConfig,
            sep2,
            menuSair
        });
    }

    private void InitializeUi()
    {
        // 1. HEADER PANEL
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        headerPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "NEXO COMPRAS AGENT",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblBadge = new Label
        {
            Text = "SINc v1.0",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            BackColor = Color.FromArgb(239, 246, 255),
            Location = new Point(220, 15),
            Padding = new Padding(6, 2, 6, 2),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Sincronizador Local de Dados ERP • Operação Contínua em Segundo Plano",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 38),
            AutoSize = true
        };

        // Quick action buttons in header
        _btnConfig = new Button
        {
            Text = "⚙️ Configurações (Token & ERP)",
            Location = new Point(610, 18),
            Size = new Size(235, 34),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnConfig.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
        _btnConfig.Click += (s, e) => AbrirTelaConfiguracoes();

        headerPanel.Controls.AddRange(new Control[] { lblTitle, lblBadge, lblSub, _btnConfig });
        Controls.Add(headerPanel);

        // 2. STATUS CARDS
        // Fileira 1: Empresa Vinculada & Filial Vinculada
        var pnlRow1 = new Panel
        {
            Location = new Point(20, 85),
            Size = new Size(825, 75),
            BackColor = Color.Transparent
        };
        var cardEmpresa = CriarCard(0, 0, 405, 75, "EMPRESA VINCULADA", out _lblEmpresaNome);
        var cardFilial = CriarCard(420, 0, 405, 75, "FILIAL VINCULADA", out _lblFilialNome);
        pnlRow1.Controls.AddRange(new Control[] { cardEmpresa, cardFilial });
        Controls.Add(pnlRow1);

        // Fileira 2: Status Internet/API, Status Banco Local, Status Sincronização
        var pnlRow2 = new Panel
        {
            Location = new Point(20, 168),
            Size = new Size(825, 75),
            BackColor = Color.Transparent
        };
        var cardNet = CriarCard(0, 0, 265, 75, "STATUS CONEXÃO / API", out _lblStatusInternet);
        var cardDb = CriarCard(280, 0, 265, 75, "BANCO DE DADOS LOCAL", out _lblStatusBanco);
        var cardSync = CriarCard(560, 0, 265, 75, "STATUS SINCRONIZAÇÃO", out _lblStatusSync);
        pnlRow2.Controls.AddRange(new Control[] { cardNet, cardDb, cardSync });
        Controls.Add(pnlRow2);

        // Fileira 3: Situação da Carga Inicial & Última Sincronização / Estatísticas
        var pnlRow3 = new Panel
        {
            Location = new Point(20, 251),
            Size = new Size(825, 65),
            BackColor = Color.Transparent
        };
        var cardCarga = CriarCard(0, 0, 310, 65, "SITUAÇÃO DA CARGA INICIAL", out _lblCargaInicial);
        var cardUltima = CriarCard(325, 0, 500, 65, "ÚLTIMA EXECUÇÃO • ESTATÍSTICAS", out _lblUltimaSync);
        pnlRow3.Controls.AddRange(new Control[] { cardCarga, cardUltima });
        Controls.Add(pnlRow3);

        // 3. ACTION BAR - BOTÃO PRINCIPAL "SINCRONIZAR" (Único e destacado)
        var pnlAction = new Panel
        {
            Location = new Point(20, 326),
            Size = new Size(825, 52),
            BackColor = Color.Transparent
        };

        _btnSincronizar = new Button
        {
            Text = "🔄 Sincronizar",
            Location = new Point(0, 5),
            Size = new Size(240, 42),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSincronizar.FlatAppearance.BorderSize = 0;
        _btnSincronizar.Click += async (s, e) => await ExecutarSincronizacaoTotal();

        _btnMinimizar = new Button
        {
            Text = "Minimizar para a Bandeja",
            Location = new Point(645, 10),
            Size = new Size(180, 34),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        _btnMinimizar.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnMinimizar.Click += (s, e) => MinimizeToTray();

        pnlAction.Controls.AddRange(new Control[] { _btnSincronizar, _btnMinimizar });
        Controls.Add(pnlAction);

        // 4. LOG DE ATIVIDADES
        var lblLog = new Label
        {
            Text = "LOG DE ATIVIDADES E SINCRONIZAÇÃO",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 388),
            AutoSize = true
        };
        Controls.Add(lblLog);

        _rtbLog = new RichTextBox
        {
            Location = new Point(20, 410),
            Size = new Size(825, 185),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_rtbLog);

        // 5. FOOTER
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.White
        };
        footerPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
        };

        _lblFooterStatus = new Label
        {
            Text = "Nexo Compras Agent ativo • Operação contínua em segundo plano",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 13),
            AutoSize = true
        };

        _chkRunInBackground = new CheckBox
        {
            Text = "Executar na bandeja ao fechar a janela",
            Checked = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(560, 12),
            AutoSize = true
        };

        footerPanel.Controls.AddRange(new Control[] { _lblFooterStatus, _chkRunInBackground });
        Controls.Add(footerPanel);
    }

    private Panel CriarCard(int x, int y, int w, int h, string titulo, out Label statusLabel)
    {
        var p = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Color.White
        };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        };

        var lblT = new Label
        {
            Text = titulo,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(12, 8),
            AutoSize = true
        };

        statusLabel = new Label
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(12, 28),
            Size = new Size(w - 24, h - 34),
            AutoEllipsis = true,
            Text = "Carregando..."
        };

        p.Controls.Add(lblT);
        p.Controls.Add(statusLabel);
        return p;
    }

    private void AtualizarDadosVisualizacao()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(AtualizarDadosVisualizacao));
            return;
        }

        _stateManager.Reload();
        var s = _stateManager.CurrentState;
        var creds = _credsManager.LoadCredentials();

        // 1. Empresa Vinculada
        if (creds != null && !string.IsNullOrWhiteSpace(creds.RazaoSocial))
        {
            _lblEmpresaNome.Text = $"{creds.RazaoSocial} (CNPJ: {(string.IsNullOrEmpty(creds.Cnpj) ? "Ativo" : creds.Cnpj)})";
        }
        else if (!string.IsNullOrWhiteSpace(s.Empresa.RazaoSocial) && s.Empresa.RazaoSocial != "Não vinculada")
        {
            _lblEmpresaNome.Text = $"{s.Empresa.RazaoSocial} (CNPJ: {s.Empresa.Cnpj})";
        }
        else
        {
            _lblEmpresaNome.Text = "Não vinculada (informe o Token da Filial)";
        }

        // 2. Filial Vinculada
        if (creds != null && !string.IsNullOrWhiteSpace(creds.FilialNome))
        {
            _lblFilialNome.Text = $"{creds.FilialNome} (Cód: {creds.FilialCodigo})";
        }
        else if (!string.IsNullOrWhiteSpace(s.Empresa.FilialNome))
        {
            _lblFilialNome.Text = $"{s.Empresa.FilialNome} (Cód: {s.Empresa.FilialCodigo})";
        }
        else
        {
            _lblFilialNome.Text = "Matriz / Padrão";
        }

        // 3. Status Internet / API
        if (s.LatenciaSaaSMs > 0)
        {
            _lblStatusInternet.Text = $"🟢 Conectado ({s.LatenciaSaaSMs} ms)";
            _lblStatusInternet.ForeColor = Color.FromArgb(22, 163, 74);
        }
        else
        {
            _lblStatusInternet.Text = $"🟡 Verificando conexão...";
            _lblStatusInternet.ForeColor = Color.FromArgb(217, 119, 6);
        }

        // 4. Status Banco Local
        if (s.Banco.Status.Contains("Conectado") || s.Banco.Status.Contains("Online"))
        {
            _lblStatusBanco.Text = $"🟢 {s.Banco.Tipo} ({s.Banco.Porta})";
            _lblStatusBanco.ForeColor = Color.FromArgb(22, 163, 74);
        }
        else
        {
            _lblStatusBanco.Text = $"🔴 {s.Banco.Status}";
            _lblStatusBanco.ForeColor = Color.FromArgb(220, 38, 38);
        }

        // 5. Status Sincronização
        _lblStatusSync.Text = s.Status == "Sincronizando" ? "⏳ Sincronizando..." : $"🟢 {s.Status}";
        _lblStatusSync.ForeColor = s.Status == "Erro" ? Color.FromArgb(220, 38, 38) : Color.FromArgb(37, 99, 235);

        // 6. Situação da Carga Inicial
        if (s.CargaInicialConcluida)
        {
            _lblCargaInicial.Text = "🟢 Carga inicial concluída";
            _lblCargaInicial.ForeColor = Color.FromArgb(22, 163, 74);
        }
        else
        {
            _lblCargaInicial.Text = "🟡 Pendente de carga inicial";
            _lblCargaInicial.ForeColor = Color.FromArgb(217, 119, 6);
        }

        // 7. Última Execução e Estatísticas
        if (s.UltimaSincronizacao.HasValue)
        {
            var dataHora = s.UltimaSincronizacao.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            if (s.UltimosProdutosEnviados > 0)
            {
                _lblUltimaSync.Text = $"{dataHora} • Novos: {s.UltimosProdutosNovos} | Modificados: {s.UltimosProdutosModificados} | Enviados: {s.UltimosProdutosEnviados}";
            }
            else
            {
                _lblUltimaSync.Text = $"{dataHora} • Sem alterações pendentes (0 enviados)";
            }
            _lblUltimaSync.ForeColor = Color.FromArgb(30, 41, 59);
        }
        else
        {
            _lblUltimaSync.Text = "Nenhuma sincronização concluída";
            _lblUltimaSync.ForeColor = Color.FromArgb(100, 116, 139);
        }

        // Atualizar Bandeja
        var filialStr = creds?.FilialNome ?? s.Empresa.FilialNome ?? "Matriz";
        var empresaStr = creds?.RazaoSocial ?? s.Empresa.RazaoSocial;
        _menuStatus.Text = $"Status: {s.Status} ({s.LatenciaSaaSMs}ms)";
        _menuEmpresa.Text = $"Empresa: {empresaStr}";
        _menuFilial.Text = $"Filial: {filialStr}";
        _menuBanco.Text = $"Banco: {s.Banco.Tipo} ({s.Banco.Status})";
        _menuUltimaSync.Text = $"Última: {(_lblUltimaSync.Text.Length > 20 ? _lblUltimaSync.Text[..20] : _lblUltimaSync.Text)}";

        _notifyIcon.Text = $"Nexo Compras Agent - {empresaStr}";
        if (_notifyIcon.Text.Length > 63) _notifyIcon.Text = _notifyIcon.Text.Substring(0, 63);

        _lblFooterStatus.Text = $"Sincronizador ativo | Última atualização: {DateTime.Now:HH:mm:ss} | Fila pendente: {_taskQueue.Count}";
    }

    private async Task ExecutarSincronizacaoTotal()
    {
        _btnSincronizar.Enabled = false;
        _btnSincronizar.Text = "⏳ Sincronizando...";
        try
        {
            LogToUi("INFO", "Iniciando ciclo completo de sincronização (Produtos, Fornecedores e Estoque)...");
            var res = await _syncEngine.SincronizarTudoAsync();
            AtualizarDadosVisualizacao();
            if (res.Sucesso)
            {
                LogToUi("SUCCESS", "Ciclo de sincronização concluído com sucesso.");
            }
            else if (res.Parcial)
            {
                LogToUi("WARN", $"Ciclo de sincronização concluído parcialmente: {res.Mensagem}");
            }
            else
            {
                LogToUi("ERROR", $"Falha no ciclo de sincronização: {res.Mensagem}");
            }
        }
        catch (Exception ex)
        {
            LogToUi("ERROR", $"Falha na sincronização: {ex.Message}");
        }
        finally
        {
            _btnSincronizar.Enabled = true;
            _btnSincronizar.Text = "🔄 Sincronizar";
        }
    }

    private async Task ExecutarTesteConectividade()
    {
        try
        {
            LogToUi("INFO", "Testando conectividade com a API SaaS e Banco ERP...");
            await _syncEngine.ProcessarCicloHeartbeatAsync();
            await _syncEngine.TestarConexaoBancoAsync();
            AtualizarDadosVisualizacao();
        }
        catch (Exception ex)
        {
            LogToUi("WARN", $"Aviso de conectividade: {ex.Message}");
        }
    }

    private async Task ExecutarCicloSegundoPlano()
    {
        try
        {
            await _syncEngine.ProcessarCicloHeartbeatAsync();
            await _syncEngine.SincronizarEstoqueAsync();
            AtualizarDadosVisualizacao();
        }
        catch (Exception ex)
        {
            LogToUi("WARN", $"Ciclo em segundo plano: {ex.Message}");
        }
    }

    private void AbrirTelaConfiguracoes()
    {
        using var cfg = new ConfigForm(_config, () =>
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            InitializeEngine();
            AtualizarDadosVisualizacao();
            LogToUi("SUCCESS", "Configurações atualizadas e aplicadas com sucesso.");
        }, _credsManager, _syncEngine, _taskQueue, _stateManager);
        cfg.ShowDialog(this);
    }

    private void AbrirTelaAtivacao()
    {
        using var actForm = new ActivationForm(_config, _credsManager, LogToUi, () =>
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            InitializeEngine();
            AtualizarDadosVisualizacao();
            LogToUi("SUCCESS", "Sincronizador vinculado com sucesso via Token Web ID Filial!");
        });
        actForm.ShowDialog(this);
    }

    private void LogToUi(string level, string msg)
    {
        if (InvokeRequired)
        {
            Invoke(() => LogToUi(level, msg));
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss");
        _rtbLog.SelectionStart = _rtbLog.TextLength;
        _rtbLog.SelectionLength = 0;

        _rtbLog.SelectionColor = Color.FromArgb(148, 163, 184);
        _rtbLog.AppendText($"[{time}] ");

        switch (level)
        {
            case "SUCCESS":
                _rtbLog.SelectionColor = Color.FromArgb(22, 163, 74);
                _rtbLog.AppendText("[OK] ");
                break;
            case "WARN":
                _rtbLog.SelectionColor = Color.FromArgb(217, 119, 6);
                _rtbLog.AppendText("[AVISO] ");
                break;
            case "ERROR":
                _rtbLog.SelectionColor = Color.FromArgb(220, 38, 38);
                _rtbLog.AppendText("[ERRO] ");
                break;
            default:
                _rtbLog.SelectionColor = Color.FromArgb(37, 99, 235);
                _rtbLog.AppendText("[INFO] ");
                break;
        }

        _rtbLog.SelectionColor = Color.FromArgb(30, 41, 59);
        _rtbLog.AppendText(msg + "\n");
        _rtbLog.ScrollToCaret();
    }

    private void MinimizeToTray()
    {
        Hide();
        _notifyIcon.ShowBalloonTip(3000, "Nexo Compras Agent", "O sincronizador continua operando silenciosamente em segundo plano.", ToolTipIcon.Info);
    }

    private void ShowAndRestore()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private void OnFormClosingHandler(object? sender, FormClosingEventArgs e)
    {
        if (!_isRealExit && _chkRunInBackground.Checked && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray();
        }
    }

    private void FecharInterface()
    {
        var result = MessageBox.Show(
            "Deseja realmente encerrar a interface do Nexo Compras Agent?\n\nO sincronizador em segundo plano continuará disponível para reabertura.",
            "Nexo Compras Agent",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (result == DialogResult.Yes)
        {
            _isRealExit = true;
            _stateRefreshTimer.Stop();
            _backgroundSyncTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Exit();
        }
    }
}
