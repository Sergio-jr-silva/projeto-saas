using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Nexo.Agent.Core.Queue;
using Nexo.Agent.Core.State;
using Nexo.Agent.Core.Sync;

namespace Nexo.Agent.Desktop;

public class DiagnosticForm : Form
{
    private readonly AgentStateManager _stateManager;
    private readonly IncrementalSyncEngine _syncEngine;
    private readonly PersistentTaskQueue _taskQueue;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    // Controls
    private Label _lblLatency = null!;
    private Label _lblDbStatus = null!;
    private Label _lblPortStatus = null!;
    private Label _lblQueueStatus = null!;
    private Label _lblSystemInfo = null!;
    private DataGridView _gridHistory = null!;
    private RichTextBox _rtbLogs = null!;
    private ComboBox _cmbLogLevel = null!;
    private Button _btnTestConn = null!;
    private Button _btnDrainQueue = null!;

    public DiagnosticForm(AgentStateManager stateManager, IncrementalSyncEngine syncEngine, PersistentTaskQueue taskQueue)
    {
        _stateManager = stateManager;
        _syncEngine = syncEngine;
        _taskQueue = taskQueue;

        Text = "Nexo Compras — Diagnóstico e Histórico do Sincronizador";
        Size = new Size(880, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(iconPath))
                this.Icon = new Icon(iconPath);
            else
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            this.Icon = SystemIcons.Application;
        }

        InitializeComponents();
        RefreshData();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _refreshTimer.Tick += (s, e) => RefreshData();
        _refreshTimer.Start();

        FormClosing += (s, e) => _refreshTimer.Stop();
    }

    private void InitializeComponents()
    {
        // HEADER
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "DIAGNÓSTICO TÉCNICO & HISTÓRICO DE OPERAÇÕES",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(20, 12),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Monitoramento de latência de rede, portas locais, fila de contingência offline e logs de auditoria",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 35),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSub });
        Controls.Add(pnlHeader);

        // TAB CONTROL
        var tabControl = new TabControl
        {
            Location = new Point(15, 75),
            Size = new Size(835, 550),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };

        // TAB 1: DIAGNÓSTICO
        var tabDiag = new TabPage("🩺 Diagnóstico do Sistema");
        tabDiag.BackColor = Color.FromArgb(248, 250, 252);
        BuildDiagnosticTab(tabDiag);

        // TAB 2: HISTÓRICO DE SINCRONIZAÇÃO
        var tabHist = new TabPage("📊 Histórico de Sincronizações");
        tabHist.BackColor = Color.FromArgb(248, 250, 252);
        BuildHistoryTab(tabHist);

        // TAB 3: LOGS DETALHADOS
        var tabLogs = new TabPage("📝 Logs em Tempo Real");
        tabLogs.BackColor = Color.FromArgb(248, 250, 252);
        BuildLogsTab(tabLogs);

        tabControl.TabPages.AddRange(new TabPage[] { tabDiag, tabHist, tabLogs });
        Controls.Add(tabControl);
    }

    private void BuildDiagnosticTab(TabPage tab)
    {
        int y = 15;

        // CARD LATENCIA E STATUS SAAS
        var grpSaas = CriarGroupBox("Conectividade Cloud SaaS (API Central)", 15, y, 790, 80);
        _lblLatency = new Label
        {
            Location = new Point(20, 25),
            Size = new Size(580, 25),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Text = "Latência SaaS: Verificando..."
        };
        _btnTestConn = new Button
        {
            Text = "⚡ Testar Conectividade",
            Location = new Point(610, 22),
            Size = new Size(160, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnTestConn.FlatAppearance.BorderSize = 0;
        _btnTestConn.Click += async (s, e) =>
        {
            _btnTestConn.Enabled = false;
            try
            {
                await _syncEngine.ProcessarCicloHeartbeatAsync();
                await _syncEngine.TestarConexaoBancoAsync();
                RefreshData();
                MessageBox.Show("Teste de conectividade concluído com sucesso!", "Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha no teste: {ex.Message}", "Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _btnTestConn.Enabled = true;
            }
        };
        grpSaas.Controls.AddRange(new Control[] { _lblLatency, _btnTestConn });
        tab.Controls.Add(grpSaas);

        y += 90;

        // CARD BANCO DE DADOS LOCAL E PORTA
        var grpDb = CriarGroupBox("Conexão com Banco de Dados ERP Local", 15, y, 790, 85);
        _lblDbStatus = new Label
        {
            Location = new Point(20, 25),
            Size = new Size(580, 22),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Text = "Banco Local: Verificando..."
        };
        _lblPortStatus = new Label
        {
            Location = new Point(20, 50),
            Size = new Size(580, 22),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "Status da Porta de Rede Local: Verificando..."
        };

        var btnDiagDbDetail = new Button
        {
            Text = "🔍 Diagnosticar Banco",
            Location = new Point(610, 22),
            Size = new Size(160, 32),
            BackColor = Color.FromArgb(239, 246, 255),
            ForeColor = Color.FromArgb(29, 78, 216),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDiagDbDetail.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
        btnDiagDbDetail.Click += async (s, e) =>
        {
            btnDiagDbDetail.Enabled = false;
            btnDiagDbDetail.Text = "⏳ Testando...";
            try
            {
                var cfgPath = Nexo.Agent.Core.Storage.SincStoragePaths.ConfigFilePath;
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddJsonFile(cfgPath, optional: true)
                    .Build();

                var dbType = config["Database:Type"] ?? "Firebird";
                if (dbType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    var p = Nexo.Agent.Core.Connectors.PostgreSqlDiagnosticParams.FromConfiguration(config);
                    var svc = new Nexo.Agent.Core.Connectors.PostgreSqlDiagnosticService();
                    var res = await svc.DiagnosticarAsync(p);
                    using var dlg = new DiagnosticResultDialog(res);
                    dlg.ShowDialog(this);
                }
                else
                {
                    var p = Nexo.Agent.Core.Connectors.FirebirdDiagnosticParams.FromConfiguration(config);
                    var svc = new Nexo.Agent.Core.Connectors.FirebirdDiagnosticService();
                    var res = await svc.DiagnosticarAsync(p);
                    using var dlg = new DiagnosticResultDialog(res);
                    dlg.ShowDialog(this);
                }
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao executar diagnóstico: {ex.Message}", "Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDiagDbDetail.Enabled = true;
                btnDiagDbDetail.Text = "🔍 Diagnosticar Banco";
            }
        };

        grpDb.Controls.AddRange(new Control[] { _lblDbStatus, _lblPortStatus, btnDiagDbDetail });
        tab.Controls.Add(grpDb);

        y += 95;

        // CARD FILA OFFLINE
        var grpQueue = CriarGroupBox("Resiliência & Fila de Tarefas Offline (Contingência)", 15, y, 790, 80);
        _lblQueueStatus = new Label
        {
            Location = new Point(20, 25),
            Size = new Size(580, 45),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Text = "Itens na fila pendente: 0 | Garantia de entrega idempotente e sem duplicatas."
        };
        _btnDrainQueue = new Button
        {
            Text = "📤 Drenar Fila Agora",
            Location = new Point(610, 22),
            Size = new Size(160, 32),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnDrainQueue.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnDrainQueue.Click += async (s, e) =>
        {
            _btnDrainQueue.Enabled = false;
            try
            {
                var processados = await _syncEngine.DrenarFilaOfflineAsync();
                RefreshData();
                MessageBox.Show($"Fila offline processada: {processados} pacotes enviados com sucesso.", "Fila Offline", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                _btnDrainQueue.Enabled = true;
            }
        };
        grpQueue.Controls.AddRange(new Control[] { _lblQueueStatus, _btnDrainQueue });
        tab.Controls.Add(grpQueue);

        y += 90;

        // CARD INFORMACOES DO SISTEMA
        var grpSys = CriarGroupBox("Informações da Estação e Ambiente de Execução", 15, y, 790, 110);
        _lblSystemInfo = new Label
        {
            Location = new Point(20, 25),
            Size = new Size(740, 75),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Text = $"Máquina: {Environment.MachineName}\n" +
                   $"Sistema Operacional: {Environment.OSVersion}\n" +
                   $"Processadores: {Environment.ProcessorCount} núcleos | .NET Runtime: {Environment.Version}\n" +
                   $"Memória do Processo: {Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 1)} MB"
        };
        grpSys.Controls.Add(_lblSystemInfo);
        tab.Controls.Add(grpSys);
    }

    private void BuildHistoryTab(TabPage tab)
    {
        _gridHistory = new DataGridView
        {
            Location = new Point(15, 15),
            Size = new Size(800, 480),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _gridHistory.Columns.Add("Data", "Data / Hora");
        _gridHistory.Columns.Add("Tipo", "Tipo Operação");
        _gridHistory.Columns.Add("Qtd", "Itens");
        _gridHistory.Columns.Add("Duracao", "Duração");
        _gridHistory.Columns.Add("Status", "Status");
        _gridHistory.Columns.Add("Mensagem", "Detalhes / Mensagem");

        _gridHistory.Columns[0].Width = 120;
        _gridHistory.Columns[1].Width = 110;
        _gridHistory.Columns[2].Width = 60;
        _gridHistory.Columns[3].Width = 70;
        _gridHistory.Columns[4].Width = 90;

        tab.Controls.Add(_gridHistory);
    }

    private void BuildLogsTab(TabPage tab)
    {
        var pnlFilter = new Panel
        {
            Location = new Point(15, 10),
            Size = new Size(800, 35),
            BackColor = Color.Transparent
        };

        var lblF = new Label
        {
            Text = "Filtrar por Nível:",
            Location = new Point(0, 8),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _cmbLogLevel = new ComboBox
        {
            Location = new Point(110, 5),
            Size = new Size(130, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbLogLevel.Items.AddRange(new object[] { "TODOS", "INFO", "SUCCESS", "WARN", "ERROR" });
        _cmbLogLevel.SelectedIndex = 0;
        _cmbLogLevel.SelectedIndexChanged += (s, e) => CarregarLogs();

        var btnClear = new Button
        {
            Text = "🧹 Limpar Visualização",
            Location = new Point(260, 4),
            Size = new Size(150, 27),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnClear.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClear.Click += (s, e) => _rtbLogs.Clear();

        pnlFilter.Controls.AddRange(new Control[] { lblF, _cmbLogLevel, btnClear });
        tab.Controls.Add(pnlFilter);

        _rtbLogs = new RichTextBox
        {
            Location = new Point(15, 50),
            Size = new Size(800, 440),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        tab.Controls.Add(_rtbLogs);
    }

    private GroupBox CriarGroupBox(string title, int x, int y, int w, int h)
    {
        return new GroupBox
        {
            Text = title,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
    }

    private void RefreshData()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(RefreshData));
            return;
        }

        _stateManager.Reload();
        var state = _stateManager.CurrentState;

        // Atualizar Latência
        if (state.LatenciaSaaSMs > 0)
        {
            var latColor = state.LatenciaSaaSMs < 300 ? Color.FromArgb(22, 163, 74) : Color.FromArgb(217, 119, 6);
            _lblLatency.Text = $"🟢 Conectado ao SaaS Cloud | Latência de Resposta: {state.LatenciaSaaSMs} ms (Excelente)";
            _lblLatency.ForeColor = latColor;
        }
        else
        {
            _lblLatency.Text = "🟡 Verificando latência com a plataforma Nexo...";
            _lblLatency.ForeColor = Color.FromArgb(180, 83, 9);
        }

        // Banco de Dados e Porta
        _lblDbStatus.Text = $"Tipo: {state.Banco.Tipo} | Status: {state.Banco.Status} | Local: {state.Banco.CaminhoOuNome}";
        _lblDbStatus.ForeColor = state.Banco.Status.Contains("Conectado") || state.Banco.Status.Contains("Simulado")
            ? Color.FromArgb(22, 163, 74)
            : Color.FromArgb(220, 38, 38);

        _lblPortStatus.Text = $"Host: {state.Banco.Host} | Porta: {state.Banco.Porta} (Socket monitorado) | Último batimento: {state.UltimaAtualizacao:HH:mm:ss}";

        // Fila Offline
        var pendentes = _taskQueue.Count;
        _lblQueueStatus.Text = pendentes > 0
            ? $"⚠️ ATENÇÃO: Existem {pendentes} pacotes acumulados na fila de contingência offline. Serão transmitidos assim que restabelecida a rede."
            : "✅ Fila offline vazia. Todos os registros locais estão 100% sincronizados com a nuvem.";
        _lblQueueStatus.ForeColor = pendentes > 0 ? Color.FromArgb(217, 119, 6) : Color.FromArgb(22, 163, 74);

        // Histórico de Sincronizações
        _gridHistory.Rows.Clear();
        foreach (var item in state.Historico)
        {
            _gridHistory.Rows.Add(
                item.Data.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                item.Tipo,
                item.QtdItens,
                $"{item.DuracaoMs} ms",
                item.Status,
                item.Mensagem
            );
        }

        CarregarLogs();
    }

    private void CarregarLogs()
    {
        var filtro = _cmbLogLevel.SelectedItem?.ToString() ?? "TODOS";
        _rtbLogs.Clear();

        var state = _stateManager.CurrentState;
        foreach (var log in state.LogsRecentes)
        {
            if (filtro != "TODOS" && !string.Equals(log.Level, filtro, StringComparison.OrdinalIgnoreCase))
                continue;

            var time = log.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            _rtbLogs.SelectionColor = Color.FromArgb(148, 163, 184);
            _rtbLogs.AppendText($"[{time}] ");

            switch (log.Level.ToUpperInvariant())
            {
                case "SUCCESS":
                    _rtbLogs.SelectionColor = Color.FromArgb(22, 163, 74);
                    _rtbLogs.AppendText("[OK] ");
                    break;
                case "WARN":
                    _rtbLogs.SelectionColor = Color.FromArgb(217, 119, 6);
                    _rtbLogs.AppendText("[AVISO] ");
                    break;
                case "ERROR":
                    _rtbLogs.SelectionColor = Color.FromArgb(220, 38, 38);
                    _rtbLogs.AppendText("[ERRO] ");
                    break;
                default:
                    _rtbLogs.SelectionColor = Color.FromArgb(37, 99, 235);
                    _rtbLogs.AppendText("[INFO] ");
                    break;
            }

            _rtbLogs.SelectionColor = Color.FromArgb(30, 41, 59);
            _rtbLogs.AppendText(log.Message + "\n");
        }
        _rtbLogs.ScrollToCaret();
    }
}
