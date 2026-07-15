using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.Threading;
using System.Threading.Tasks;
using WmosAutomatizacion.Classes;
using WmosAutomatizacionNew.Helpers;
using FlaUIApplication = FlaUI.Core.Application;    // Alias para FlaUI
using FormsApp = System.Windows.Forms.Application; // Alias para Windows Forms



namespace WmosAutomatizacion.Flujos
{
    internal class Login
    {
        public static async Task FlujoLogin(FlaUIApplication app, UIA3Automation automation)
        {
            try
            {
                var ventanaLogin = app.GetMainWindow(automation);
                Console.WriteLine("Apertura ventana Login: " + ventanaLogin?.Title);

                if (ventanaLogin == null)
                {
                    Console.WriteLine("No se encontró la ventana de login.");
                    Kisoft.CerrarAplicacion(app, automation, "error");
                    return;
                }

                IngresarCredenciales(ventanaLogin);
                Kisoft.PulsarBotonInvoke(app, automation, ventanaLogin, "buttonLogin");

                var watcher = new LoginWatcher();
                var respuestaLogin = await watcher.EsperarResultadoLoginAsync(app, automation, TimeSpan.FromSeconds(30), CancellationToken.None);

                if (respuestaLogin.ventanaPrincipal == null)
                {
                    Console.WriteLine("❌ No se pudo completar el login.");
                    Kisoft.CerrarAplicacion(app, automation, "error");
                    return;
                }

                Console.WriteLine("✅ Login Completado.");
            }
            catch (Exception e)
            {
                Console.WriteLine("❌ Error inesperado en Login.");
                Console.WriteLine($"❌ Error: {e.Message}");
                Console.WriteLine($"❌ Error: {e}");
                Kisoft.CerrarAplicacion(app, automation, "error");
            }
        }


        public static void IngresarCredenciales(Window ventana)
        {
            ventana.FindFirstDescendant(cf => cf.ByAutomationId("textBoxUser"))?.AsTextBox().Enter("cl17457391");    // Falabella
            ventana.FindFirstDescendant(cf => cf.ByAutomationId("textBoxPassword"))?.AsTextBox().Enter("Tradis9.");  // falabella
            ventana.FindFirstDescendant(cf => cf.ByAutomationId("1001"))?.AsTextBox().Enter("10.81.118.66:11659");         // 10.81.146.21
        }
    }
}
