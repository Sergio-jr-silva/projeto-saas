using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Nexo.Agent.Core.Connectors;

namespace Nexo.Agent.Desktop;

public class DiagnosticResultDialog : Form
{
    private readonly string _tipoBanco;
    private readonly bool _sucesso;
    private readonly string _statusBadge;
    private readonly long _elapsedMs;
    private readonly string _host;
    private readonly int _port;
    private readonly string _database;
    private readonly string _user;
    private readonly string _sslMode;
    private readonly int _timeout;
    private readonly string _etapaFalha;
    private readonly string _msgTecnica;
    private readonly string _sugestao;
    private readonly string? _versao;
    private readonly string[] _logs;

    public DiagnosticResultDialog(PostgreSqlDiagnosticResult pg)
    {
        _tipoBanco = "PostgreSQL";
        _sucesso = pg.Sucesso;
        _statusBadge = pg.StatusBadge;
        _elapsedMs = pg.ElapsedMilliseconds;
        _host = pg.Host;
        _port = pg.Port;
        _database = pg.Database;
        _user = pg.User;
        _sslMode = pg.SslMode;
        _timeout = pg.TimeoutSeconds;
        _etapaFalha = pg.EtapaFalha;
        _msgTecnica = pg.MensagemTecnica;
        _sugestao = pg.SugestaoDiagnostico;
        _versao = pg.VersaoPostgreSql;
        _logs = pg.LogsEtapas.ToArray();

        InitializeUI();
    }

    public DiagnosticResultDialog(FirebirdDiagnosticResult fb)
    {
        _tipoBanco = "Firebird";
        _sucesso = fb.Sucesso;
        _statusBadge = fb.StatusBadge;
        _elapsedMs = fb.ElapsedMilliseconds;
        _host = fb.Host;
        _port = fb.Port;
        _database = fb.Database;
        _user = fb.User;
        _sslMode = "N/A";
        _timeout = 15;
        _etapaFalha = fb.EtapaFalha;
        _msgTecnica = fb.MensagemTecnica;
        _sugestao = fb.SugestaoDiagnostico;
        _versao = fb.VersaoFirebird;
        _logs = fb.LogsEtapas.ToArray();

        InitializeUI();
    }

    private void InitializeUI()
    {
        Text = $"Resultado do Diagnóstico Técnico — {_tipoBanco}";
        Size = new Size(680, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        // Header Banner
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = _sucesso ? Color.FromArgb(240, 253, 244) : Color.FromArgb(254, 242, 242),
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(_sucesso ? Color.FromArgb(187, 247, 208) : Color.FromArgb(254, 202, 202));
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var badgeColor = _statusBadge switch
        {
            "Conectado" => Color.FromArgb(22, 163, 74),
            "Servidor acessível" => Color.FromArgb(217, 119, 6),
            "Problema SSL" => Color.FromArgb(234, 88, 12),
            "Campos inválidos" => Color.FromArgb(217, 119, 6),
            _ => Color.FromArgb(220, 38, 38)
        };

        var lblBadge = new Label
        {
            Text = $"STATUS: {_statusBadge.ToUpperInvariant()}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = badgeColor,
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblSummary = new Label
        {
            Text = _sucesso
                ? $"Conexão validada em {_elapsedMs} ms. Todas as etapas de resolução, porta TCP e autenticação foram aprovadas."
                : $"Falha identificada na etapa '{_etapaFalha}'. Tempo de resposta: {_elapsedMs} ms.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 38),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange(new Control[] { lblBadge, lblSummary });
        Controls.Add(pnlHeader);

        // Content Area
        var pnlBody = new Panel
        {
            Location = new Point(15, 85),
            Size = new Size(635, 435),
            AutoScroll = true
        };

        int y = 5;

        // Card Parâmetros e Versão
        var grpParams = new GroupBox
        {
            Text = "Parâmetros e Ambiente Verificado",
            Location = new Point(5, y),
            Size = new Size(625, 95),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        var lblP1 = new Label
        {
            Location = new Point(15, 22),
            Size = new Size(595, 20),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(51, 65, 85),
            Text = $"• Servidor: {_host}:{_port}   |   Banco: {_database}   |   Usuário: {_user}"
        };

        var sslInfo = _tipoBanco == "PostgreSQL" ? $"   |   SSL: {_sslMode}   |   Timeout: {_timeout}s" : "";
        var lblP2 = new Label
        {
            Location = new Point(15, 42),
            Size = new Size(595, 20),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(51, 65, 85),
            Text = $"• Motor: {_tipoBanco}{sslInfo}   |   Latência: {_elapsedMs} ms"
        };

        var lblP3 = new Label
        {
            Location = new Point(15, 62),
            Size = new Size(595, 25),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = _sucesso ? Color.FromArgb(22, 163, 74) : Color.FromArgb(100, 116, 139),
            Text = $"• Versão do Banco: {_versao ?? "Não identificada"}"
        };

        grpParams.Controls.AddRange(new Control[] { lblP1, lblP2, lblP3 });
        pnlBody.Controls.Add(grpParams);
        y += 105;

        // Card de Erro / Sugestão se falhou
        if (!_sucesso)
        {
            var grpDiag = new GroupBox
            {
                Text = "Diagnóstico do Problema & Sugestão de Correção",
                Location = new Point(5, y),
                Size = new Size(625, 120),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(185, 28, 28)
            };

            var lblErro = new Label
            {
                Location = new Point(15, 22),
                Size = new Size(595, 32),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(127, 29, 29),
                Text = $"Mensagem Técnica: {_msgTecnica}"
            };

            var pnlSugestao = new Panel
            {
                Location = new Point(15, 56),
                Size = new Size(595, 52),
                BackColor = Color.FromArgb(254, 243, 199),
                Padding = new Padding(8)
            };

            var lblSugestao = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(146, 64, 14),
                Text = $"💡 Sugestão de Correção:\n{_sugestao}"
            };
            pnlSugestao.Controls.Add(lblSugestao);

            grpDiag.Controls.AddRange(new Control[] { lblErro, pnlSugestao });
            pnlBody.Controls.Add(grpDiag);
            y += 130;
        }

        // Logs das etapas detalhadas
        var grpLogs = new GroupBox
        {
            Text = "Registro Passo a Passo das Etapas de Diagnóstico",
            Location = new Point(5, y),
            Size = new Size(625, !_sucesso ? 185 : 305),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        var rtb = new RichTextBox
        {
            Location = new Point(12, 22),
            Size = new Size(600, !_sucesso ? 150 : 270),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Consolas", 8.5f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };

        foreach (var l in _logs)
        {
            rtb.AppendText(l + "\n");
        }
        rtb.ScrollToCaret();

        grpLogs.Controls.Add(rtb);
        pnlBody.Controls.Add(grpLogs);

        Controls.Add(pnlBody);

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

        var btnCopiar = new Button
        {
            Text = "📋 Copiar Relatório Técnico",
            Location = new Point(20, 12),
            Size = new Size(200, 32),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            Cursor = Cursors.Hand
        };
        btnCopiar.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
        btnCopiar.Click += (s, e) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== RELATÓRIO DE DIAGNÓSTICO DO BANCO DE DADOS ERP ({_tipoBanco}) ===");
            sb.AppendLine($"Data/Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Status: {_statusBadge}");
            sb.AppendLine($"Latência: {_elapsedMs} ms");
            sb.AppendLine($"Host/Porta: {_host}:{_port}");
            sb.AppendLine($"Banco: {_database}");
            sb.AppendLine($"Usuário: {_user}");
            if (_tipoBanco == "PostgreSQL")
            {
                sb.AppendLine($"Modo SSL: {_sslMode}");
                sb.AppendLine($"Timeout: {_timeout}s");
            }
            sb.AppendLine($"Versão do Banco: {_versao ?? "N/A"}");
            if (!_sucesso)
            {
                sb.AppendLine($"Etapa da Falha: {_etapaFalha}");
                sb.AppendLine($"Mensagem Técnica: {_msgTecnica}");
                sb.AppendLine($"Sugestão de Resolução: {_sugestao}");
            }
            sb.AppendLine("\n--- LOGS DETALHADOS DAS ETAPAS ---");
            foreach (var l in _logs) sb.AppendLine(l);

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Relatório de diagnóstico copiado para a área de transferência!", "Copiado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnFechar = new Button
        {
            Text = "Fechar",
            Location = new Point(545, 12),
            Size = new Size(100, 32),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        btnFechar.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnFechar.Click += (s, e) => Close();

        pnlBottom.Controls.AddRange(new Control[] { btnCopiar, btnFechar });
        Controls.Add(pnlBottom);
    }
}

