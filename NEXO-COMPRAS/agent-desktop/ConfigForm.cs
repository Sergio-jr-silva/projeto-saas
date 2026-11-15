using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Nexo.Agent.Core.Connectors;
using Nexo.Agent.Core.Queue;
using Nexo.Agent.Core.Security;
using Nexo.Agent.Core.State;
using Nexo.Agent.Core.Sync;

namespace Nexo.Agent.Desktop;

public class ConfigForm : Form
{
    private readonly string _configFilePath;
    private readonly IConfiguration _config;
    private readonly Action _onSaved;
    private readonly AgentCredentialsManager? _credsManager;
    private readonly IncrementalSyncEngine? _syncEngine;
    private readonly PersistentTaskQueue? _taskQueue;
    private readonly AgentStateManager? _stateManager;

    // Identificação Controls
    private Label _lblIdentEmpresa = null!;
    private Label _lblIdentFilial = null!;
    private Label _lblIdentAgentId = null!;
    private Label _lblIdentTokenStatus = null!;
    private Label _lblStatusApi = null!;
    private Label _lblStatusDb = null!;
    private TextBox _txtApiUrl = null!;
    private TextBox _txtTokenIntegracao = null!;
    private TextBox _txtCnpjBase = null!;
    private TextBox _txtAgentKey = null!;
    private Button _btnValidarToken = null!;

    // Database Controls
    private ComboBox _cmbDbType = null!;
    private GroupBox _grpFb = null!;
    private TextBox _txtFbHost = null!;
    private NumericUpDown _numFbPort = null!;
    private TextBox _txtFbDb = null!;
    private TextBox _txtFbUser = null!;
    private TextBox _txtFbPass = null!;
    private TextBox _txtFbCharset = null!;
    private Button _btnTestFb = null!;

    private GroupBox _grpPg = null!;
    private TextBox _txtPgHost = null!;
    private NumericUpDown _numPgPort = null!;
    private TextBox _txtPgDb = null!;
    private TextBox _txtPgUser = null!;
    private TextBox _txtPgPass = null!;
    private TextBox _txtPgSchema = null!;
    private ComboBox _cmbPgSslMode = null!;
    private NumericUpDown _numPgTimeout = null!;
    private Button _btnTestPg = null!;
    private Button _btnAutoDetectSchema = null!;
    private Label _lblSchemaDetectionResult = null!;

    // Sincronização Controls
    private NumericUpDown _numEstoqueMin = null!;
    private NumericUpDown _numProdutosMin = null!;
    private NumericUpDown _numFornecedoresMin = null!;
    private NumericUpDown _numHeartbeatSeg = null!;
    private Label _lblBackgroundServiceStatus = null!;

    // Diagnóstico Controls
    private Button _btnDiagTestDb = null!;
    private Button _btnDiagTestSaas = null!;
    private RichTextBox _rtbDiagOutput = null!;

    public ConfigForm(
        IConfiguration config,
        Action onSaved,
        AgentCredentialsManager? credsManager = null,
        IncrementalSyncEngine? syncEngine = null,
        PersistentTaskQueue? taskQueue = null,
        AgentStateManager? stateManager = null)
    {
        _config = config;
        _onSaved = onSaved;
        _credsManager = credsManager;
        _syncEngine = syncEngine;
        _taskQueue = taskQueue;
        _stateManager = stateManager;
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

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

        Text = "Nexo Compras — Configurações do Agente (SINc)";
        Size = new Size(760, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        InitializeComponents();
        CarregarValores();
    }

    private void InitializeComponents()
    {
        // Header
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
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
            Text = "PAINEL DE CONFIGURAÇÕES & INTEGRAÇÃO DO SINc",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(20, 10),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Configuração centralizada por Token e CNPJ-base, conexão ERP e parâmetros de sincronização",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 32),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSub });
        Controls.Add(pnlHeader);

        // Tab Control
        var tabControl = new TabControl
        {
            Location = new Point(15, 72),
            Size = new Size(715, 510)
        };

        // 1. Identificação
        var tabIdent = new TabPage("🏢 Identificação & Token");
        tabIdent.BackColor = Color.FromArgb(248, 250, 252);
        BuildIdentTab(tabIdent);

        // 2. Banco de Dados
        var tabDb = new TabPage("🗄️ Banco de Dados ERP");
        tabDb.BackColor = Color.FromArgb(248, 250, 252);
        BuildDbTab(tabDb);

        // 3. Sincronização
        var tabSync = new TabPage("⏱️ Sincronização");
        tabSync.BackColor = Color.FromArgb(248, 250, 252);
        BuildSyncTab(tabSync);

        // 4. Diagnóstico
        var tabDiag = new TabPage("🩺 Diagnóstico");
        tabDiag.BackColor = Color.FromArgb(248, 250, 252);
        BuildDiagTab(tabDiag);

        tabControl.TabPages.AddRange(new TabPage[] { tabIdent, tabDb, tabSync, tabDiag });
        Controls.Add(tabControl);

        // Bottom Actions
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.White
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnSalvar = new Button
        {
            Text = "💾 Salvar Configurações",
            Location = new Point(530, 12),
            Size = new Size(195, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSalvar.FlatAppearance.BorderSize = 0;
        btnSalvar.Click += (s, e) => SalvarConfiguracoes();

        var btnCancelar = new Button
        {
            Text = "Fechar",
            Location = new Point(435, 12),
            Size = new Size(85, 34),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        btnCancelar.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancelar.Click += (s, e) => Close();

        pnlBottom.Controls.AddRange(new Control[] { btnCancelar, btnSalvar });
        Controls.Add(pnlBottom);
    }

    private void BuildIdentTab(TabPage tab)
    {
        // 1. Grupo de Configuração por Token e CNPJ-base
        var grpAuth = new GroupBox
        {
            Text = "Credenciais de Integração (Token e CNPJ-Base da Empresa)",
            Location = new Point(15, 12),
            Size = new Size(680, 190),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        grpAuth.Controls.Add(new Label { Text = "Token de Integração da Filial (gerado no painel Web): *", Location = new Point(20, 25), AutoSize = true });
        _txtTokenIntegracao = new TextBox
        {
            Location = new Point(20, 45),
            Size = new Size(450, 24),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        grpAuth.Controls.Add(_txtTokenIntegracao);

        grpAuth.Controls.Add(new Label { Text = "CNPJ-Base (8 dígitos):", Location = new Point(485, 25), AutoSize = true });
        _txtCnpjBase = new TextBox
        {
            Location = new Point(485, 45),
            Size = new Size(175, 24),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        grpAuth.Controls.Add(_txtCnpjBase);

        grpAuth.Controls.Add(new Label { Text = "URL Base da Plataforma SaaS (API Central):", Location = new Point(20, 80), AutoSize = true });
        _txtApiUrl = new TextBox { Location = new Point(20, 100), Size = new Size(450, 23) };
        grpAuth.Controls.Add(_txtApiUrl);

        _btnValidarToken = new Button
        {
            Text = "🔑 Validar Token e Conectar",
            Location = new Point(485, 96),
            Size = new Size(175, 30),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnValidarToken.FlatAppearance.BorderSize = 0;
        _btnValidarToken.Click += async (s, e) => await ValidarTokenEConectar();
        grpAuth.Controls.Add(_btnValidarToken);

        var lblDica = new Label
        {
            Text = "💡 O Token da Filial identifica automaticamente a empresa e a unidade autorizada no backend.",
            Location = new Point(20, 138),
            Size = new Size(640, 36),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8f)
        };
        grpAuth.Controls.Add(lblDica);

        tab.Controls.Add(grpAuth);

        // 2. Grupo de Identificação Confirmada pelo Backend
        var grpInfo = new GroupBox
        {
            Text = "Status da Autenticação & Identificação Autorizada",
            Location = new Point(15, 210),
            Size = new Size(680, 250),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        int y = 25;
        grpInfo.Controls.Add(new Label { Text = "Status da Autenticação:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _lblIdentTokenStatus = new Label { Location = new Point(190, y), Size = new Size(465, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "Carregando..." };
        grpInfo.Controls.Add(_lblIdentTokenStatus);

        y += 30;
        grpInfo.Controls.Add(new Label { Text = "Empresa Identificada:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _lblIdentEmpresa = new Label { Location = new Point(190, y), Size = new Size(465, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "Carregando..." };
        grpInfo.Controls.Add(_lblIdentEmpresa);

        y += 30;
        grpInfo.Controls.Add(new Label { Text = "Filial Identificada:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _lblIdentFilial = new Label { Location = new Point(190, y), Size = new Size(465, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "Carregando..." };
        grpInfo.Controls.Add(_lblIdentFilial);

        y += 30;
        grpInfo.Controls.Add(new Label { Text = "Conexão com API SaaS:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _lblStatusApi = new Label { Location = new Point(190, y), Size = new Size(465, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "Verificando..." };
        grpInfo.Controls.Add(_lblStatusApi);

        y += 30;
        grpInfo.Controls.Add(new Label { Text = "Conexão com Banco ERP:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _lblStatusDb = new Label { Location = new Point(190, y), Size = new Size(465, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "Verificando..." };
        grpInfo.Controls.Add(_lblStatusDb);

        y += 30;
        grpInfo.Controls.Add(new Label { Text = "ID da Instalação (Agente):", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _lblIdentAgentId = new Label { Location = new Point(190, y), Size = new Size(465, 20), Font = new Font("Segoe UI", 8.5f), Text = "Não configurado" };
        grpInfo.Controls.Add(_lblIdentAgentId);

        y += 28;
        grpInfo.Controls.Add(new Label { Text = "Credencial Interna (DPAPI):", Location = new Point(20, y), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        _txtAgentKey = new TextBox { Location = new Point(190, y - 3), Size = new Size(465, 21), ReadOnly = true, UseSystemPasswordChar = true };
        grpInfo.Controls.Add(_txtAgentKey);

        tab.Controls.Add(grpInfo);
    }

    private void BuildDbTab(TabPage tab)
    {
        var lblTipo = new Label { Text = "Tipo de Banco de Dados ERP:", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbDbType = new ComboBox
        {
            Location = new Point(20, 36),
            Size = new Size(230, 26),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbDbType.Items.AddRange(new object[] { "Firebird", "PostgreSQL" });
        _cmbDbType.SelectedIndexChanged += (s, e) => AlternarGruposDb();

        _btnAutoDetectSchema = new Button
        {
            Text = "🔍 Identificar Estrutura do Banco (Auto-Detect)",
            Location = new Point(270, 34),
            Size = new Size(320, 28),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAutoDetectSchema.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
        _btnAutoDetectSchema.Click += async (s, e) => await ExecutarDescobertaSchema();

        tab.Controls.AddRange(new Control[] { lblTipo, _cmbDbType, _btnAutoDetectSchema });

        // GROUP FIREBIRD
        _grpFb = new GroupBox
        {
            Text = "Parâmetros Firebird (Exclusivo ERP Local)",
            Location = new Point(15, 75),
            Size = new Size(680, 215),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        int fy = 25;
        _grpFb.Controls.Add(new Label { Text = "Host / IP:", Location = new Point(15, fy), AutoSize = true });
        _txtFbHost = new TextBox { Location = new Point(80, fy - 3), Size = new Size(160, 23) };
        _grpFb.Controls.Add(_txtFbHost);

        _grpFb.Controls.Add(new Label { Text = "Porta:", Location = new Point(265, fy), AutoSize = true });
        _numFbPort = new NumericUpDown { Location = new Point(315, fy - 3), Size = new Size(80, 23), Maximum = 65535, Value = 3050 };
        _grpFb.Controls.Add(_numFbPort);

        _grpFb.Controls.Add(new Label { Text = "Charset:", Location = new Point(420, fy), AutoSize = true });
        _txtFbCharset = new TextBox { Location = new Point(480, fy - 3), Size = new Size(175, 23), Text = "NONE" };
        _grpFb.Controls.Add(_txtFbCharset);

        fy += 38;
        _grpFb.Controls.Add(new Label { Text = "Caminho do Banco (.FDB):", Location = new Point(15, fy), AutoSize = true });
        _txtFbDb = new TextBox { Location = new Point(15, fy + 20), Size = new Size(535, 23) };

        var btnBrowseFb = new Button
        {
            Text = "📂 Selecionar...",
            Location = new Point(555, fy + 19),
            Size = new Size(105, 26),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        btnBrowseFb.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnBrowseFb.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Selecionar Banco de Dados Firebird (.FDB)",
                Filter = "Bancos Firebird (*.fdb;*.gdb)|*.fdb;*.gdb|Todos os arquivos (*.*)|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _txtFbDb.Text = ofd.FileName;
            }
        };
        _grpFb.Controls.AddRange(new Control[] { _txtFbDb, btnBrowseFb });

        fy += 52;
        _grpFb.Controls.Add(new Label { Text = "Usuário:", Location = new Point(15, fy), AutoSize = true });
        _txtFbUser = new TextBox { Location = new Point(80, fy - 3), Size = new Size(160, 23) };
        _grpFb.Controls.Add(_txtFbUser);

        _grpFb.Controls.Add(new Label { Text = "Senha:", Location = new Point(265, fy), AutoSize = true });
        _txtFbPass = new TextBox { Location = new Point(315, fy - 3), Size = new Size(160, 23), UseSystemPasswordChar = true };
        _grpFb.Controls.Add(_txtFbPass);

        fy += 40;
        _btnTestFb = new Button
        {
            Text = "🔌 Executar Diagnóstico de Conectividade Firebird",
            Location = new Point(15, fy),
            Size = new Size(645, 32),
            BackColor = Color.FromArgb(240, 253, 244),
            ForeColor = Color.FromArgb(21, 128, 61),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnTestFb.FlatAppearance.BorderColor = Color.FromArgb(187, 247, 208);
        _btnTestFb.Click += async (s, e) => await ExecutarDiagnosticoFb();
        _grpFb.Controls.Add(_btnTestFb);

        tab.Controls.Add(_grpFb);

        // GROUP POSTGRESQL
        _grpPg = new GroupBox
        {
            Text = "Parâmetros PostgreSQL (Exclusivo ERP Local)",
            Location = new Point(15, 75),
            Size = new Size(680, 215),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Visible = false
        };

        int py = 25;
        _grpPg.Controls.Add(new Label { Text = "Host / IP:", Location = new Point(15, py), AutoSize = true });
        _txtPgHost = new TextBox { Location = new Point(75, py - 3), Size = new Size(130, 23) };
        _grpPg.Controls.Add(_txtPgHost);

        _grpPg.Controls.Add(new Label { Text = "Porta:", Location = new Point(215, py), AutoSize = true });
        _numPgPort = new NumericUpDown { Location = new Point(255, py - 3), Size = new Size(65, 23), Maximum = 65535, Value = 5432 };
        _grpPg.Controls.Add(_numPgPort);

        _grpPg.Controls.Add(new Label { Text = "Schema:", Location = new Point(330, py), AutoSize = true });
        _txtPgSchema = new TextBox { Location = new Point(385, py - 3), Size = new Size(80, 23), Text = "public" };
        _grpPg.Controls.Add(_txtPgSchema);

        _grpPg.Controls.Add(new Label { Text = "SSL:", Location = new Point(475, py), AutoSize = true });
        _cmbPgSslMode = new ComboBox
        {
            Location = new Point(505, py - 3),
            Size = new Size(80, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbPgSslMode.Items.AddRange(new object[] { "Prefer", "Require", "Disable", "VerifyCA", "VerifyFull" });
        _cmbPgSslMode.SelectedIndex = 0;
        _grpPg.Controls.Add(_cmbPgSslMode);

        _grpPg.Controls.Add(new Label { Text = "Timeout:", Location = new Point(595, py), AutoSize = true });
        _numPgTimeout = new NumericUpDown { Location = new Point(645, py - 3), Size = new Size(40, 23), Minimum = 1, Maximum = 300, Value = 15 };
        _grpPg.Controls.Add(_numPgTimeout);

        py += 38;
        _grpPg.Controls.Add(new Label { Text = "Nome do Banco de Dados:", Location = new Point(15, py), AutoSize = true });
        _txtPgDb = new TextBox { Location = new Point(15, py + 20), Size = new Size(650, 23) };
        _grpPg.Controls.Add(_txtPgDb);

        py += 52;
        _grpPg.Controls.Add(new Label { Text = "Usuário:", Location = new Point(15, py), AutoSize = true });
        _txtPgUser = new TextBox { Location = new Point(75, py - 3), Size = new Size(160, 23) };
        _grpPg.Controls.Add(_txtPgUser);

        _grpPg.Controls.Add(new Label { Text = "Senha:", Location = new Point(265, py), AutoSize = true });
        _txtPgPass = new TextBox { Location = new Point(315, py - 3), Size = new Size(160, 23), UseSystemPasswordChar = true };
        _grpPg.Controls.Add(_txtPgPass);

        py += 40;
        _btnTestPg = new Button
        {
            Text = "🔌 Executar Diagnóstico de Conectividade PostgreSQL",
            Location = new Point(15, py),
            Size = new Size(650, 32),
            BackColor = Color.FromArgb(239, 246, 255),
            ForeColor = Color.FromArgb(29, 78, 216),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnTestPg.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
        _btnTestPg.Click += async (s, e) => await ExecutarDiagnosticoPg();
        _grpPg.Controls.Add(_btnTestPg);

        tab.Controls.Add(_grpPg);

        // Grupo Resultado da Identificação do Schema
        var grpSchema = new GroupBox
        {
            Text = "Mapeamento Identificado das Entidades do ERP",
            Location = new Point(15, 300),
            Size = new Size(680, 160),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        _lblSchemaDetectionResult = new Label
        {
            Location = new Point(15, 25),
            Size = new Size(650, 145),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(51, 65, 85),
            Text = "Mapeamento de tabelas e colunas será carregado ao salvar ou ao clicar em 'Identificar Estrutura do Banco'."
        };
        grpSchema.Controls.Add(_lblSchemaDetectionResult);
        tab.Controls.Add(grpSchema);
    }

    private void AlternarGruposDb()
    {
        var tipo = _cmbDbType.SelectedItem?.ToString() ?? "Firebird";
        var isFb = tipo.Equals("Firebird", StringComparison.OrdinalIgnoreCase);
        _grpFb.Visible = isFb;
        _grpPg.Visible = !isFb;
    }

    private void BuildSyncTab(TabPage tab)
    {
        var grpInt = new GroupBox
        {
            Text = "Intervalos de Sincronização Periódica",
            Location = new Point(15, 15),
            Size = new Size(680, 220),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        int iy = 30;
        grpInt.Controls.Add(new Label { Text = "Sincronização de Estoque (minutos):", Location = new Point(20, iy), AutoSize = true });
        _numEstoqueMin = new NumericUpDown { Location = new Point(280, iy - 3), Size = new Size(80, 23), Minimum = 1, Maximum = 1440, Value = 5 };
        grpInt.Controls.Add(_numEstoqueMin);

        iy += 40;
        grpInt.Controls.Add(new Label { Text = "Sincronização de Produtos (minutos):", Location = new Point(20, iy), AutoSize = true });
        _numProdutosMin = new NumericUpDown { Location = new Point(280, iy - 3), Size = new Size(80, 23), Minimum = 1, Maximum = 1440, Value = 30 };
        grpInt.Controls.Add(_numProdutosMin);

        iy += 40;
        grpInt.Controls.Add(new Label { Text = "Sincronização de Fornecedores (minutos):", Location = new Point(20, iy), AutoSize = true });
        _numFornecedoresMin = new NumericUpDown { Location = new Point(280, iy - 3), Size = new Size(80, 23), Minimum = 1, Maximum = 1440, Value = 30 };
        grpInt.Controls.Add(_numFornecedoresMin);

        iy += 40;
        grpInt.Controls.Add(new Label { Text = "Batimento de Vida / Heartbeat (segundos):", Location = new Point(20, iy), AutoSize = true });
        _numHeartbeatSeg = new NumericUpDown { Location = new Point(280, iy - 3), Size = new Size(80, 23), Minimum = 10, Maximum = 3600, Value = 60 };
        grpInt.Controls.Add(_numHeartbeatSeg);

        tab.Controls.Add(grpInt);

        var grpServico = new GroupBox
        {
            Text = "Status da Operação em Segundo Plano",
            Location = new Point(15, 250),
            Size = new Size(680, 160),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        _lblBackgroundServiceStatus = new Label
        {
            Location = new Point(20, 30),
            Size = new Size(640, 110),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Text = "O Nexo Compras Agent executa silenciosamente na bandeja do sistema.\nMesmo ao fechar a janela principal, as rotinas em segundo plano continuam coletando dados do ERP local e enviando para a plataforma web conforme a periodicidade configurada acima."
        };
        grpServico.Controls.Add(_lblBackgroundServiceStatus);
        tab.Controls.Add(grpServico);
    }

    private void BuildDiagTab(TabPage tab)
    {
        var lblDesc = new Label
        {
            Text = "Execute testes técnicos de conectividade para validar a comunicação entre o banco local e a nuvem:",
            Location = new Point(15, 15),
            Size = new Size(680, 30),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        tab.Controls.Add(lblDesc);

        _btnDiagTestDb = new Button
        {
            Text = "🗄️ Testar Conexão com Banco Local",
            Location = new Point(15, 50),
            Size = new Size(260, 36),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(37, 99, 235),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnDiagTestDb.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
        _btnDiagTestDb.Click += async (s, e) => await ExecutarTesteDb();

        _btnDiagTestSaas = new Button
        {
            Text = "☁️ Testar Conexão com Nexo SaaS",
            Location = new Point(290, 50),
            Size = new Size(250, 36),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(22, 163, 74),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnDiagTestSaas.FlatAppearance.BorderColor = Color.FromArgb(187, 247, 208);
        _btnDiagTestSaas.Click += async (s, e) => await ExecutarTesteSaas();

        tab.Controls.AddRange(new Control[] { _btnDiagTestDb, _btnDiagTestSaas });

        _rtbDiagOutput = new RichTextBox
        {
            Location = new Point(15, 100),
            Size = new Size(680, 350),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        tab.Controls.Add(_rtbDiagOutput);
    }

    private async Task ValidarTokenEConectar()
    {
        var token = _txtTokenIntegracao.Text.Trim();
        var cnpjBase = _txtCnpjBase.Text.Trim();
        var apiUrl = _txtApiUrl.Text.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Por favor, informe o Token de Integração da Filial.", "Token Obrigatório", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            MessageBox.Show("Por favor, informe a URL da Plataforma SaaS.", "URL Obrigatória", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnValidarToken.Enabled = false;
        _btnValidarToken.Text = "⏳ Validando...";

        try
        {
            if (_credsManager == null)
            {
                MessageBox.Show("Gerenciador de credenciais não disponível.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var (sucesso, msg) = await _credsManager.AtivarComTokenECnpjBaseAsync(apiUrl, token, cnpjBase);
            if (sucesso)
            {
                CarregarValores();
                MessageBox.Show($"Token validado com sucesso!\n\n{msg}", "Integração Concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Não foi possível autenticar o token:\n\n{msg}", "Falha de Autenticação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro na validação do token: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnValidarToken.Enabled = true;
            _btnValidarToken.Text = "🔑 Validar Token e Conectar";
        }
    }

    private async Task ExecutarDescobertaSchema()
    {
        _btnAutoDetectSchema.Enabled = false;
        _btnAutoDetectSchema.Text = "⏳ Analisando tabelas...";
        try
        {
            var dbType = _cmbDbType.SelectedItem?.ToString() ?? "Firebird";
            IDatabaseConnector connector = dbType == "Firebird"
                ? new FirebirdConnector(_config)
                : new PostgreSqlConnector(_config);

            var ok = await connector.TestConnectionAsync();
            if (!ok)
            {
                MessageBox.Show("Não foi possível conectar ao banco de dados ERP especificado.\nVerifique o host, porta e arquivo do banco antes de executar a descoberta.", "Falha de Conexão", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var mapping = await connector.DiscoverAndApplySchemaAsync(true);
            ExibirResultadoMapeamento(mapping);
            MessageBox.Show("Estrutura do banco de dados identificada e mapeada com sucesso!", "Auto-Detect Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro na identificação da estrutura: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnAutoDetectSchema.Enabled = true;
            _btnAutoDetectSchema.Text = "🔍 Identificar Estrutura do Banco (Auto-Detect)";
        }
    }

    private void ExibirResultadoMapeamento(Core.Schema.ResolvedTableMapping m)
    {
        var prodStatus = m.Produtos.Valido ? $"✅ Tabela '{m.Produtos.TableName}' (Cód: {m.Produtos.CodigoCol}, Nome: {m.Produtos.DescricaoCol}, Preço: {m.Produtos.PrecoVendaCol})" : $"❌ {m.Produtos.MotivoInvalido}";
        var estTab = m.Estoque.HasDedicatedTable ? $"Tabela dedicada '{m.Estoque.TableName}'" : $"Direto em '{m.Produtos.TableName}'";
        var estStatus = m.Estoque.Valido ? $"✅ {estTab} (Campo Quantidade: {m.Estoque.EstoqueAtualCol}, Pendência: {m.Estoque.EstoquePendenteCol ?? "N/A"})" : $"❌ {m.Estoque.MotivoInvalido}";
        var fornStatus = m.Fornecedores.Valido ? $"✅ Tabela '{m.Fornecedores.TableName}' (Cód: {m.Fornecedores.CodigoCol}, Nome: {m.Fornecedores.RazaoSocialCol}, CNPJ: {m.Fornecedores.CnpjCol})" : $"❌ {m.Fornecedores.MotivoInvalido}";

        _lblSchemaDetectionResult.Text =
            $"• Produtos: {prodStatus}\n\n" +
            $"• Estoque: {estStatus}\n\n" +
            $"• Fornecedores: {fornStatus}\n\n" +
            $"Última identificação: {m.IdentificadoEm:dd/MM/yyyy HH:mm:ss} (Banco: {m.DatabaseType})";
    }

    private async Task ExecutarDiagnosticoPg()
    {
        _btnTestPg.Enabled = false;
        _btnTestPg.Text = "⏳ Executando Diagnóstico PostgreSQL...";
        try
        {
            var p = new PostgreSqlDiagnosticParams
            {
                Host = _txtPgHost.Text.Trim(),
                Port = (int)_numPgPort.Value,
                Database = _txtPgDb.Text.Trim(),
                User = _txtPgUser.Text.Trim(),
                Password = _txtPgPass.Text,
                SslMode = _cmbPgSslMode.SelectedItem?.ToString() ?? "Prefer",
                TimeoutSeconds = (int)_numPgTimeout.Value,
                Schema = _txtPgSchema.Text.Trim()
            };

            var svc = new PostgreSqlDiagnosticService();
            var res = await svc.DiagnosticarAsync(p);

            using var dlg = new DiagnosticResultDialog(res);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro durante a execução do diagnóstico: {ex.Message}", "Erro de Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnTestPg.Enabled = true;
            _btnTestPg.Text = "🔌 Executar Diagnóstico de Conectividade PostgreSQL";
        }
    }

    private async Task ExecutarDiagnosticoFb()
    {
        _btnTestFb.Enabled = false;
        _btnTestFb.Text = "⏳ Executando Diagnóstico Firebird...";
        try
        {
            var p = new FirebirdDiagnosticParams
            {
                Host = _txtFbHost.Text.Trim(),
                Port = (int)_numFbPort.Value,
                Database = _txtFbDb.Text.Trim(),
                User = _txtFbUser.Text.Trim(),
                Password = _txtFbPass.Text,
                Charset = _txtFbCharset.Text.Trim(),
                TimeoutSeconds = 15
            };

            var svc = new FirebirdDiagnosticService();
            var res = await svc.DiagnosticarAsync(p);

            using var dlg = new DiagnosticResultDialog(res);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro durante a execução do diagnóstico: {ex.Message}", "Erro de Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnTestFb.Enabled = true;
            _btnTestFb.Text = "🔌 Executar Diagnóstico de Conectividade Firebird";
        }
    }

    private async Task ExecutarTesteDb()
    {
        _btnDiagTestDb.Enabled = false;
        var tipo = _cmbDbType.SelectedItem?.ToString() ?? "Firebird";
        AppendDiag($"Iniciando diagnóstico detalhado de conexão com o Banco ERP ({tipo})...");
        try
        {
            if (tipo.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var p = new PostgreSqlDiagnosticParams
                {
                    Host = _txtPgHost.Text.Trim(),
                    Port = (int)_numPgPort.Value,
                    Database = _txtPgDb.Text.Trim(),
                    User = _txtPgUser.Text.Trim(),
                    Password = _txtPgPass.Text,
                    SslMode = _cmbPgSslMode.SelectedItem?.ToString() ?? "Prefer",
                    TimeoutSeconds = (int)_numPgTimeout.Value,
                    Schema = _txtPgSchema.Text.Trim()
                };
                var svc = new PostgreSqlDiagnosticService();
                var res = await svc.DiagnosticarAsync(p);
                foreach (var log in res.LogsEtapas)
                {
                    AppendDiag(log);
                }
                if (res.Sucesso)
                {
                    AppendDiag($"🟢 SUCESSO: Conexão PostgreSQL estabelecida ({res.ElapsedMilliseconds} ms)! Versão: {res.VersaoPostgreSql}");
                }
                else
                {
                    AppendDiag($"🔴 FALHA [{res.StatusBadge}]: {res.MensagemTecnica}");
                    AppendDiag($"💡 SUGESTÃO: {res.SugestaoDiagnostico}");
                }
            }
            else
            {
                var p = new FirebirdDiagnosticParams
                {
                    Host = _txtFbHost.Text.Trim(),
                    Port = (int)_numFbPort.Value,
                    Database = _txtFbDb.Text.Trim(),
                    User = _txtFbUser.Text.Trim(),
                    Password = _txtFbPass.Text,
                    Charset = _txtFbCharset.Text.Trim(),
                    TimeoutSeconds = 15
                };
                var svc = new FirebirdDiagnosticService();
                var res = await svc.DiagnosticarAsync(p);
                foreach (var log in res.LogsEtapas)
                {
                    AppendDiag(log);
                }
                if (res.Sucesso)
                {
                    AppendDiag($"🟢 SUCESSO: Conexão Firebird estabelecida ({res.ElapsedMilliseconds} ms)! Versão: {res.VersaoFirebird}");
                }
                else
                {
                    AppendDiag($"🔴 FALHA [{res.StatusBadge}]: {res.MensagemTecnica}");
                    AppendDiag($"💡 SUGESTÃO: {res.SugestaoDiagnostico}");
                }
            }
        }
        catch (Exception ex)
        {
            AppendDiag($"🔴 ERRO INESPERADO: {ex.Message}");
        }
        finally
        {
            _btnDiagTestDb.Enabled = true;
        }
    }

    private async Task ExecutarTesteSaas()
    {
        _btnDiagTestSaas.Enabled = false;
        AppendDiag("Iniciando teste de conectividade com a API Central Nexo SaaS...");
        try
        {
            if (_syncEngine != null)
            {
                var ok = await _syncEngine.ProcessarCicloHeartbeatAsync();
                if (ok)
                    AppendDiag("🟢 SUCESSO: Comunicação com a nuvem autorizada (Heartbeat OK).");
                else
                    AppendDiag("🟡 AVISO: A API respondeu com falha ou credencial não autorizada.");
            }
            else
            {
                AppendDiag("🟡 Sincronizador não inicializado.");
            }
        }
        catch (Exception ex)
        {
            AppendDiag($"🔴 ERRO: {ex.Message}");
        }
        finally
        {
            _btnDiagTestSaas.Enabled = true;
        }
    }

    private void AppendDiag(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}\n";
        _rtbDiagOutput.AppendText(line);
        _rtbDiagOutput.ScrollToCaret();
    }

    private void CarregarValores()
    {
        // Identificação via credenciais DPAPI
        var creds = _credsManager?.LoadCredentials();
        if (creds != null)
        {
            _lblIdentEmpresa.Text = $"{creds.RazaoSocial} (CNPJ: {creds.Cnpj})";
            _lblIdentFilial.Text = string.IsNullOrEmpty(creds.FilialNome) ? "Matriz" : $"{creds.FilialNome} ({creds.FilialCodigo})";
            _lblIdentAgentId.Text = creds.AgentId.ToString();
            _lblIdentTokenStatus.Text = "🟢 Token Ativo e Protegido com Windows DPAPI";
            _lblIdentTokenStatus.ForeColor = Color.FromArgb(22, 163, 74);
            _txtAgentKey.Text = creds.AgentCredential;

            if (creds.Cnpj.Length >= 8)
            {
                var dig = new string(creds.Cnpj.Where(char.IsDigit).Take(8).ToArray());
                _txtCnpjBase.Text = dig;
            }
        }
        else
        {
            _lblIdentEmpresa.Text = "Aguardando validação do token";
            _lblIdentFilial.Text = "Aguardando validação do token";
            _lblIdentAgentId.Text = "Não gerado";
            _lblIdentTokenStatus.Text = "🟡 Não autenticado";
            _lblIdentTokenStatus.ForeColor = Color.FromArgb(217, 119, 6);
            _txtAgentKey.Text = "";
        }

        if (_stateManager != null)
        {
            var s = _stateManager.Current;
            _lblStatusApi.Text = $"{s.Status} ({s.LatenciaSaaSMs} ms)";
            _lblStatusApi.ForeColor = s.Status == "Online" ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);

            _lblStatusDb.Text = $"{s.Banco.Tipo} ({s.Banco.Status})";
            _lblStatusDb.ForeColor = s.Banco.Status.Contains("Conectado") || s.Banco.Status.Contains("Sucesso") ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);
        }

        // Tipo DB
        _cmbDbType.SelectedItem = _config["Database:Type"] ?? "Firebird";

        // Firebird
        _txtFbHost.Text = _config["Firebird:Host"] ?? "127.0.0.1";
        _numFbPort.Value = int.TryParse(_config["Firebird:Port"], out var fp) ? fp : 3050;
        _txtFbDb.Text = _config["Firebird:Database"] ?? @"C:\DATACASH\BM.FDB";
        _txtFbUser.Text = _config["Firebird:User"] ?? "SYSDBA";
        _txtFbPass.Text = _config["Firebird:Password"] ?? "masterkey";
        _txtFbCharset.Text = _config["Firebird:Charset"] ?? "NONE";

        // Postgres
        _txtPgHost.Text = _config["PostgreSql:Host"] ?? "127.0.0.1";
        _numPgPort.Value = int.TryParse(_config["PostgreSql:Port"], out var pp) ? pp : 5432;
        _txtPgDb.Text = _config["PostgreSql:Database"] ?? "sistema_comercial";
        _txtPgUser.Text = _config["PostgreSql:User"] ?? "postgres";
        _txtPgPass.Text = _config["PostgreSql:Password"] ?? "postgres";
        _txtPgSchema.Text = _config["PostgreSql:Schema"] ?? "public";
        _cmbPgSslMode.SelectedItem = _config["PostgreSql:SslMode"] ?? "Prefer";
        if (_cmbPgSslMode.SelectedIndex < 0) _cmbPgSslMode.SelectedIndex = 0;
        _numPgTimeout.Value = int.TryParse(_config["PostgreSql:Timeout"], out var pt) ? pt : 15;

        // SaaS
        _txtApiUrl.Text = _config["NexoApi:BaseUrl"] ?? "http://localhost:5000";

        // Intervals
        _numEstoqueMin.Value = int.TryParse(_config["Sync:IntervaloEstoqueMin"], out var em) ? em : 5;
        _numProdutosMin.Value = int.TryParse(_config["Sync:IntervaloProdutosMin"], out var pm) ? pm : 30;
        _numFornecedoresMin.Value = int.TryParse(_config["Sync:IntervaloFornecedoresMin"], out var fm) ? fm : 30;
        _numHeartbeatSeg.Value = int.TryParse(_config["Sync:IntervaloHeartbeatSeg"], out var hs) ? hs : 60;

        AlternarGruposDb();

        // Carrega mapeamento do schema em cache se existir
        var store = new Core.Schema.SchemaMappingStore();
        var cachedMapping = store.LoadMapping();
        if (cachedMapping != null && cachedMapping.Produtos.Valido)
        {
            ExibirResultadoMapeamento(cachedMapping);
        }
    }

    private void SalvarConfiguracoes()
    {
        try
        {
            var jsonText = File.Exists(_configFilePath) ? File.ReadAllText(_configFilePath) : "{}";
            var root = JsonNode.Parse(jsonText)?.AsObject() ?? new JsonObject();

            // Database
            if (!root.ContainsKey("Database")) root["Database"] = new JsonObject();
            root["Database"]!["Type"] = _cmbDbType.SelectedItem?.ToString() ?? "Firebird";

            // Firebird
            if (!root.ContainsKey("Firebird")) root["Firebird"] = new JsonObject();
            var fb = root["Firebird"]!.AsObject();
            fb["Host"] = _txtFbHost.Text;
            fb["Port"] = (int)_numFbPort.Value;
            fb["Database"] = _txtFbDb.Text;
            fb["User"] = _txtFbUser.Text;
            fb["Password"] = _txtFbPass.Text;
            fb["Charset"] = _txtFbCharset.Text;

            // Postgres
            if (!root.ContainsKey("PostgreSql")) root["PostgreSql"] = new JsonObject();
            var pg = root["PostgreSql"]!.AsObject();
            pg["Host"] = _txtPgHost.Text;
            pg["Port"] = (int)_numPgPort.Value;
            pg["Database"] = _txtPgDb.Text;
            pg["User"] = _txtPgUser.Text;
            pg["Password"] = _txtPgPass.Text;
            pg["Schema"] = _txtPgSchema.Text;
            pg["SslMode"] = _cmbPgSslMode.SelectedItem?.ToString() ?? "Prefer";
            pg["Timeout"] = (int)_numPgTimeout.Value;

            // SaaS
            if (!root.ContainsKey("NexoApi")) root["NexoApi"] = new JsonObject();
            var api = root["NexoApi"]!.AsObject();
            api["BaseUrl"] = _txtApiUrl.Text;

            // Sync
            if (!root.ContainsKey("Sync")) root["Sync"] = new JsonObject();
            var sync = root["Sync"]!.AsObject();
            sync["IntervaloEstoqueMin"] = (int)_numEstoqueMin.Value;
            sync["IntervaloProdutosMin"] = (int)_numProdutosMin.Value;
            sync["IntervaloFornecedoresMin"] = (int)_numFornecedoresMin.Value;
            sync["IntervaloHeartbeatSeg"] = (int)_numHeartbeatSeg.Value;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_configFilePath, root.ToJsonString(options));

            _onSaved();
            MessageBox.Show("Configurações atualizadas e salvas com sucesso!", "Nexo Compras", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar configurações: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
