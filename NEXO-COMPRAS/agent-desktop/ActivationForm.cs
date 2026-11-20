using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Nexo.Agent.Core.Security;

namespace Nexo.Agent.Desktop;

public class ActivationForm : Form
{
    private readonly IConfiguration _config;
    private readonly AgentCredentialsManager _credsManager;
    private readonly Action<string, string>? _logger;
    private readonly Action _onActivated;

    private TextBox _txtApiUrl = null!;
    private TextBox _txtCode = null!;
    private Button _btnActivate = null!;
    private Label _lblStatus = null!;

    public ActivationForm(IConfiguration config, AgentCredentialsManager credsManager, Action<string, string>? logger, Action onActivated)
    {
        _config = config;
        _credsManager = credsManager;
        _logger = logger;
        _onActivated = onActivated;

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

        Text = "Nexo Compras — Vincular Filial (Token Web ID Filial)";
        Size = new Size(580, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Header
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
            Text = "VINCULAR FILIAL & AUTENTICAR SINCRONIZADOR",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Location = new Point(20, 10),
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Autenticação exclusiva por Filial com credenciais protegidas via Windows DPAPI",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 33),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSub });
        Controls.Add(pnlHeader);

        // Body Content
        int y = 80;

        var lblDesc = new Label
        {
            Text = "Informe o Token do Web ID Filial gerado no painel Web (Central de Integração ou Módulo Filiais). Cada filial possui um token exclusivo que garante o isolamento seguro dos dados.",
            Location = new Point(25, y),
            Size = new Size(515, 36),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        Controls.Add(lblDesc);

        y += 42;

        var lblApi = new Label
        {
            Text = "Endereço da Plataforma Central (SaaS):",
            Location = new Point(25, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _txtApiUrl = new TextBox
        {
            Location = new Point(25, y + 20),
            Size = new Size(515, 23),
            Text = _config["NexoApi:BaseUrl"] ?? "http://localhost:5000"
        };
        Controls.AddRange(new Control[] { lblApi, _txtApiUrl });

        y += 55;

        var lblCode = new Label
        {
            Text = "Token Web ID Filial (ex: FLK-... ou ID da Filial):",
            Location = new Point(25, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _txtCode = new TextBox
        {
            Location = new Point(25, y + 20),
            Size = new Size(515, 28),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            CharacterCasing = CharacterCasing.Upper,
            PlaceholderText = "Cole aqui o Token da Filial (ex: FLK-8B91...) ou ID"
        };
        Controls.AddRange(new Control[] { lblCode, _txtCode });

        y += 58;

        _lblStatus = new Label
        {
            Location = new Point(25, y),
            Size = new Size(515, 30),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "Status: Aguardando Token da Filial para ativação segura."
        };
        Controls.Add(_lblStatus);

        y += 35;

        // Bottom panel
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

        _btnActivate = new Button
        {
            Text = "🔑 Vincular e Ativar Filial",
            Location = new Point(320, 12),
            Size = new Size(220, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnActivate.FlatAppearance.BorderSize = 0;
        _btnActivate.Click += async (s, e) => await ExecutarAtivacao();

        var btnCancelar = new Button
        {
            Text = "Cancelar",
            Location = new Point(220, 12),
            Size = new Size(90, 32),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnCancelar.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancelar.Click += (s, e) => Close();

        pnlBottom.Controls.AddRange(new Control[] { btnCancelar, _btnActivate });
        Controls.Add(pnlBottom);
    }

    private async Task ExecutarAtivacao()
    {
        var codigo = _txtCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            MessageBox.Show("Por favor, informe o Token Web ID Filial ou o identificador da filial.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var baseUrl = _txtApiUrl.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "http://localhost:5000";

        _btnActivate.Enabled = false;
        _lblStatus.Text = "🟡 Validando Token com a plataforma central...";
        _lblStatus.ForeColor = Color.FromArgb(217, 119, 6);

        var (sucesso, mensagem) = await _credsManager.AtivarComCodigoAsync(baseUrl, codigo, _logger);

        if (sucesso)
        {
            _lblStatus.Text = "🟢 " + mensagem;
            _lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
            MessageBox.Show(mensagem, "Vinculação Concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _onActivated();
            Close();
        }
        else
        {
            _lblStatus.Text = "🔴 " + mensagem;
            _lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
            MessageBox.Show(mensagem, "Falha na Vinculação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnActivate.Enabled = true;
        }
    }
}
