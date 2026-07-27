using System;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace TwZapret
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Ловим любые ошибки интерфейса
            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Ошибка интерфейса:\n{args.Exception.Message}", "TwZapret Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // 2. Ловим фатальные системные ошибки
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show($"Фатальная ошибка:\n{args.ExceptionObject}", "TwZapret Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            try
            {
                // Стандартный запуск WPF (он сам откроет одно окно)
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при старте программы:\n{ex.Message}\n\n{ex.StackTrace}", "TwZapret Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
            }
        }
    }
}