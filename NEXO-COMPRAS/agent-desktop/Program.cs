using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Nexo.Agent.Core.Storage;

namespace Nexo.Agent.Desktop;

internal static class Program
{
    private const string MutexId = @"Local\NexoCompras_SINc_SingleInstance_Mutex";
    private static readonly string LogDir = SincStoragePaths.LogsDirectory;
    private static readonly string LogPath = SincStoragePaths.ErrorLogPath;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    [STAThread]
    private static void Main(string[] args)
    {
        // Garante criação de todos os diretórios em %ProgramData%\NexoCompras\SINc
        SincStoragePaths.EnsureDirectories();

        // Garante suporte a páginas de códigos legadas como WIN1252 / ISO-8859-1
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
        catch { }

        // Mecanismo de Instância Única (Named Mutex com tratamento para fechamento inesperado)
        bool createdNew;
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(true, MutexId, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            // O processo anterior encerrou inesperadamente: assume posse do mutex com segurança
            createdNew = true;
        }

        if (!createdNew)
        {
            // Já existe uma instância em execução: traz a janela existente para frente e encerra esta
            try
            {
                var hWnd = FindWindow(null, "Nexo Compras — Sincronizador ERP (SINc)");
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                }
            }
            catch { }

            MessageBox.Show(
                "O Nexo Compras SINc já está em execução neste computador.",
                "Nexo Compras SINc",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        try
        {
            // Configuração de tratamento global de exceções
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => LogException("ThreadException", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    LogException("UnhandledException", ex);
            };

            ApplicationConfiguration.Initialize();

            try
            {
                var basePath = SincStoragePaths.BaseDirectory;
                var config = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                var form = new MainForm(config);

                if (args.Length > 0 && (args[0] == "--minimized" || args[0] == "--tray"))
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.ShowInTaskbar = false;
                }

                Application.Run(form);
            }
            catch (Exception ex)
            {
                LogException("MainStartupException", ex);
                MessageBox.Show(
                    $"Ocorreu um erro ao inicializar o Nexo Compras Agent:\n\n{ex.Message}\n\nDetalhes foram registrados em {LogPath}.",
                    "Nexo Compras Agent — Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        finally
        {
            try
            {
                mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
            catch { }
        }
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(LogPath, logEntry);
        }
        catch { }
    }
}
