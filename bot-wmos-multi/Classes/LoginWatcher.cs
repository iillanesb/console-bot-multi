using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WmosAutomatizacion
{
    public class LoginWatcher
    {
        public enum LoginResult
        {
            Exitoso,
            Fallido,
            Timeout,
            Error
        }

        public class Respuesta
        {
            public LoginResult estado { get; set; }
            public Window ventanaPrincipal { get; set; }
        }

        public async Task<Respuesta> EsperarResultadoLoginAsync(Application app, UIA3Automation automation, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                var reloj = Stopwatch.StartNew();

                do
                {
                    await Task.Delay(5000, cancellationToken);

                    var ventanas = app.GetAllTopLevelWindows(automation);

                    var ventanaPrincipal = ventanas.FirstOrDefault(w => w.AutomationId == "KiMainWindowView");
                    if (ventanaPrincipal != null)
                    {
                        return new Respuesta
                        {
                            estado = LoginResult.Exitoso,
                            ventanaPrincipal = ventanaPrincipal
                        };
                    }

                    var ventanaError = ventanas.FirstOrDefault(w => w.AutomationId == "TitleBar");
                    if (ventanaError != null)
                    {
                        return new Respuesta
                        {
                            estado = LoginResult.Fallido,
                            ventanaPrincipal = null
                        };
                    }

                } while (!cancellationToken.IsCancellationRequested && reloj.Elapsed < timeout);

                return new Respuesta
                {
                    estado = LoginResult.Timeout,
                    ventanaPrincipal = null
                };
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Error Login watcher");
                Console.WriteLine($"❌ Error: {e}");

                return new Respuesta
                {
                    estado = LoginResult.Fallido,
                    ventanaPrincipal = null
                };
            }
        }
    }
}
