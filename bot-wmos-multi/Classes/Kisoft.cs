using System;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Threading;
using FlaUIApplication = FlaUI.Core.Application;
using System.Diagnostics;
using System.IO;
using FlaUI.Core.Input;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FlaUI.Core.Tools;
using System.Windows.Forms;
using FlaUI.Core.WindowsAPI;

namespace WmosAutomatizacionNew.Helpers
{
    class Kisoft
    {
        public static void PulsarBotonInvoke(FlaUIApplication app, UIA3Automation automation, Window ventana, string name)
        {
            var boton = ventana.FindFirstDescendant(cf => cf.ByAutomationId(name))?.AsButton();

            if (boton == null)
            {
                Console.WriteLine($"ERROR No se encontró el botón con AutomationId: {name}");
                CerrarAplicacion(app, automation, "error");
            }

            if (!boton.IsEnabled)
            {
                Console.WriteLine($"El botón con AutomationId: {name} está deshabilitado.");
                CerrarAplicacion(app, automation, "error");
            }

            try
            {
                boton.Invoke();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error al invocar el botón '{name}': {e.Message}");
                Console.WriteLine($"Error: {e}");
                CerrarAplicacion(app, automation, "error");
            }
        }

        public static void PulsarBotonClick(FlaUIApplication app, UIA3Automation automation, Window ventana, string tipo, string Name)
        {
            Thread.Sleep(2000);
            AutomationElement boton = null;

            switch (tipo.ToLower())
            {
                case "name":
                    boton = ventana.FindFirstDescendant(cf => cf.ByName(Name));
                    break;
                case "id":
                    boton = ventana.FindFirstDescendant(cf => cf.ByAutomationId(Name));
                    break;
                default:
                    Console.WriteLine($"Tipo de búsqueda no soportado: {tipo}");
                    return;
            }

            if (boton != null)
            {
                Thread.Sleep(1000);
                boton.Click();
            }
            else
            {
                Console.WriteLine($"No se encontró el botón con nombre '{Name}'");
                CerrarAplicacion(app, automation, "error");
            }
        }

        public static void CerrarAplicacion(FlaUIApplication app, UIA3Automation automation, string tipoFin)
        {
            Console.WriteLine("--");
            Console.WriteLine("Proceso Kisoft finalizado.");
            automation?.Dispose();
            if (app != null && !app.HasExited) app.Close(); // Cierra la aplicación de Kisoft

            if (tipoFin == "error")
            {
                //ReiniciarAplicacion(); // <- reinicia automáticamente
                return; // Esto evita que se siga ejecutando el Exit(1)
            }
            Environment.Exit(1); // Finaliza inmediatamente la aplicación de consola si no hubo error
        }

        public static void ReiniciarAplicacion()
        {
            var exePath = Process.GetCurrentProcess().MainModule.FileName;

            if (!File.Exists(exePath)) // si no lo encuentra dinamicamente
            {
                exePath = @"C:\ProyectosConsola\Kisoft\ConsoleKisoft\ConsoleKisoft\bin\Release\ConsoleKisoft.exe";
            }

            Process.Start(exePath);
            Environment.Exit(0); // Finaliza la instancia actual
        }



        public static bool AceptarAdvertenciaSeguro(FlaUIApplication app, UIA3Automation automation, int timeout = 10)
        {
            var desktop = app.GetMainWindow(automation);
            //var desktop = automation.GetDesktop();
            var end = DateTime.Now.AddSeconds(timeout);

            while (DateTime.Now < end)
            {
                var boton = desktop.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(
                          cf.ByName("Aceptar")
                          .Or(cf.ByName("OK"))
                          .Or(cf.ByName("Si"))
                          .Or(cf.ByName("Sí"))
                      )
                );

                if (boton != null)
                {
                    Console.WriteLine($"✅ Botón encontrado: {boton.Name}");

                    try
                    {
                        boton.Focus();
                        //Thread.Sleep(200);

                        if (boton.Patterns.Invoke.IsSupported)
                        {
                            boton.Patterns.Invoke.Pattern.Invoke();
                            Console.WriteLine("✅ Invoke OK");
                        }
                        else
                        {
                            boton.Click();
                            Console.WriteLine("✅ Click OK");
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error: {ex.Message}");
                    }
                }

                Thread.Sleep(500);
            }

            return false;
        }

        public static bool ClickBotonPorArea( FlaUIApplication app, UIA3Automation automation, int timeout = 10)
        {
            var window = app.GetMainWindow(automation);
            window.Focus();

            var end = DateTime.Now.AddSeconds(timeout);

            while (DateTime.Now < end)
            {
                var botones = window.FindAllDescendants(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                );

                foreach (var b in botones)
                {
                    try
                    {
                        var rect = b.BoundingRectangle;

   

                        if (rect.Height > 80 && rect.Width > 200)
                        {
                            Console.WriteLine($"✅ Botón correcto encontrado por tamaño: {rect}");

                            if (b.Patterns.Invoke.IsSupported)
                            {
                                b.Patterns.Invoke.Pattern.Invoke();
                                return true;
                            }

                            if (b.TryGetClickablePoint(out var point))
                            {
                                Mouse.MoveTo(point);
                                Thread.Sleep(100);
                                Mouse.Click(MouseButton.Left);
                                return true;
                            }
                        }
                    }
                    catch { }
                }
            }

            return false;
        }


        public static AutomationElement BuscarTextoUltraRobusto(
    AutomationElement root,
    string texto)
        {
            var elementos = root.FindAllDescendants();

            foreach (var e in elementos)
            {
                try
                {

                    if (!string.IsNullOrEmpty(e.Name) &&
                        e.Name.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine($"✅ Encontrado en: {e.ControlType} → {e.Name}");
                        return e;
                    }

                }
                catch { }
            }

            return null;
        }



        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;
        private const int SW_MAXIMIZE = 3;
        //public static async Task<bool> ClickPorCoordenadasSeguro(
        //    FlaUIApplication app,
        //    UIA3Automation automation,
        //    int x,
        //    int y,
        //    int delay = 2000)
        //{
        //    try
        //    {
        //        var window = app.GetMainWindow(automation);
        //        const int SW_MAXIMIZE = 3;
        //        ShowWindow(window.Properties.NativeWindowHandle, SW_MAXIMIZE);
        //        //ShowWindow(window.Properties.NativeWindowHandle, SW_RESTORE);
        //        SetForegroundWindow(window.Properties.NativeWindowHandle);
        //        window.Focus();

        //        Thread.Sleep(500);

        //        var punto = new System.Drawing.Point(x, y);

        //        Console.WriteLine($"✅ Click seguro en: {x}, {y}");

        //        Mouse.MoveTo(punto);
        //        Thread.Sleep(delay);
        //        Mouse.Click(MouseButton.Left);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error: {ex.Message}");
        //        return false;
        //    }
        //}


        public static async Task<bool> ClickPorCoordenadasSeguro(
    FlaUIApplication app,
    UIA3Automation automation,
    int x,
    int y,
    int delay = 200)
        {
            try
            {
                var window = app.GetMainWindow(automation);

                if (window == null)
                    throw new Exception("No se encontró la ventana principal");

                

                // 🔥 Asegurar ventana en foreground + maximizada
                ShowWindow(window.Properties.NativeWindowHandle, SW_MAXIMIZE);
                SetForegroundWindow(window.Properties.NativeWindowHandle);
                window.Focus();

                // 🔥 Esperar a que realmente esté lista
                await EsperarVentanaLista(window);

                var punto = new System.Drawing.Point(x, y);

                Console.WriteLine($"✅ Click seguro en: {x}, {y}");

                Mouse.MoveTo(punto);
                await Task.Delay(delay);
                Mouse.Click(MouseButton.Left);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return false;
            }
        }


        public static async Task EsperarVentanaLista(Window window, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();

            // 🔹 1. Esperar que no esté offscreen
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!window.IsOffscreen)
                    break;

                await Task.Delay(200);
            }

            // 🔹 2. Esperar que esté maximizada real
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    if (window.Patterns.Window.IsSupported)
                    {
                        var state = window.Patterns.Window.Pattern.WindowVisualState;

                        if (state == FlaUI.Core.Definitions.WindowVisualState.Maximized)
                            break;
                    }
                }
                catch { }

                await Task.Delay(200);
            }

            // 🔹 3. Esperar estabilidad visual (ULTRA IMPORTANTE)
            var lastRect = window.BoundingRectangle;
            int stableTime = 0;

            while (stableTime < 800)
            {
                await Task.Delay(200);

                var currentRect = window.BoundingRectangle;

                if (currentRect.Equals(lastRect))
                {
                    stableTime += 200;
                }
                else
                {
                    stableTime = 0;
                    lastRect = currentRect;
                }
            }

            Console.WriteLine("✅ Ventana lista y estable");
        }


        public static async Task<bool> CambiarValorTextboxPorValorActualAsync( FlaUIApplication app, UIA3Automation automation, string valorActual, string nuevoValor, int timeout = 10000 )
        {
            try
            {
                var window = app.GetMainWindow(automation);

                await EsperarVentanaLista(window);

                var sw = Stopwatch.StartNew();
                AutomationElement textbox = null;

                while (sw.ElapsedMilliseconds < timeout)
                {
                    var textboxes = window.FindAllDescendants(cf =>
                        cf.ByAutomationId("PART_Editor")
                          .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))
                    );

                    foreach (var tb in textboxes)
                    {
                        try
                        {
                            if (tb.Patterns.Value.IsSupported)
                            {

                                var valorRaw = tb.Patterns.Value.Pattern.Value;

                                string valor = valorRaw == null
                                    ? ""
                                    : valorRaw.ToString().Trim();

                                if (!string.IsNullOrEmpty(valor) && valor == valorActual)
                                {
                                    textbox = tb;
                                }

                            }
                        }
                        catch { }
                    }

                    if (textbox != null)
                        break;

                    await Task.Delay(200);
                }

                if (textbox == null)
                    throw new Exception($"No se encontró textbox con valor '{valorActual}'");

                textbox.Focus();
                await Task.Delay(150);

                if (textbox.Patterns.Value.IsSupported)
                {
                    textbox.Patterns.Value.Pattern.SetValue(nuevoValor);
                }
                else
                {
                    textbox.Click();
                    await Task.Delay(150);

                    Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);
                    Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                    Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                    Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);

                    await Task.Delay(100);
                    Keyboard.Type(nuevoValor);
                }


                textbox.Focus();
                // Presionar enter
                await Task.Delay(200);
                //Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
                Keyboard.TypeSimultaneously(
                    new[] { FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER }
                );


                await Task.Delay(500);
                Console.WriteLine($"✅ Valor cambiado de {valorActual} → {nuevoValor}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> ClickDerechoEnCeldaPorValorAsync( FlaUIApplication app, UIA3Automation automation, string valorBuscado, int timeout = 10000)
        {
            try
            {

                await Task.Delay(500);
                var window = app.GetMainWindow(automation);

                var sw = Stopwatch.StartNew();
                AutomationElement celda = null;


                ShowWindow(window.Properties.NativeWindowHandle, SW_MAXIMIZE);
                SetForegroundWindow(window.Properties.NativeWindowHandle);
                window.Focus();


                // 🔥 click en la grilla (MUY IMPORTANTE)
                var center = window.BoundingRectangle.Center();
                Mouse.MoveTo(center);
                Mouse.Click(MouseButton.Left);


                while (sw.ElapsedMilliseconds < timeout)
                {
                    var celdas = window.FindAllDescendants(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Custom)
                    );

                    //foreach (var c in celdas)
                    //{
                    //    try
                    //    {
                    //        if (c.Patterns.Value.IsSupported)
                    //        {
                    //            string valor = c.Patterns.LegacyIAccessible.Pattern.Value;

                    //            if (!string.IsNullOrEmpty(valor) && valor == valorBuscado)
                    //            {
                    //                celda = c;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    catch { }
                    //}


                    foreach (var c in celdas)
                    {
                        try
                        {
                            Console.WriteLine("----- ELEMENTO -----");

                            Console.WriteLine($"IsOffscreen: {c.Properties.IsOffscreen.Value}");
                            Console.WriteLine($"Name: [{c.Properties.Name.Value}]");

                            string legacy = null;
                            string valuePattern = null;

                            if (c.Patterns.LegacyIAccessible.IsSupported)
                            {
                                try
                                {
                                    legacy = c.Patterns.LegacyIAccessible.Pattern.Value;
                                }
                                catch (Exception ex)
                                {
                                    legacy = $"ERROR: {ex.Message}";
                                }
                            }
                            else
                            {
                                legacy = "NO SOPORTADO";
                            }

                            if (c.Patterns.Value.IsSupported)
                            {
                                try
                                {
                                    valuePattern = c.Patterns.Value.Pattern.Value;
                                }
                                catch (Exception ex)
                                {
                                    valuePattern = $"ERROR: {ex.Message}";
                                }
                            }
                            else
                            {
                                valuePattern = "NO SOPORTADO";
                            }

                            Console.WriteLine($"LegacyValue: [{legacy}]");
                            Console.WriteLine($"ValuePattern: [{valuePattern}]");

                            Console.WriteLine("--------------------");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR leyendo elemento: {ex.Message}");
                        }
                    }


                    foreach (var c in celdas)
                    {
                        try
                        {
                            if (c.Properties.IsOffscreen.Value)
                                continue;

                            string valor = null;

                            // ✅ lee Legacy SIEMPRE si existe (sin depender de ValuePattern)
                            if (c.Patterns.LegacyIAccessible.IsSupported)
                            {
                                try
                                {
                                    valor = c.Patterns.LegacyIAccessible.Pattern.Value;
                                }
                                catch { }
                            }

                            // ✅ fallback a ValuePattern
                            if (string.IsNullOrEmpty(valor) && c.Patterns.Value.IsSupported)
                            {
                                try
                                {
                                    valor = c.Patterns.Value.Pattern.Value;
                                }
                                catch { }
                            }

                            valor = valor?.Trim();

                            Console.WriteLine($"DEBUG -> [{valor}]");

                            if (!string.IsNullOrEmpty(valor) && valor == valorBuscado)
                            {
                                celda = c;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (celda != null)
                        break;

                    await Task.Delay(200);
                }

                if (celda == null)
                    throw new Exception($"No se encontró celda con valor '{valorBuscado}'");

                ShowWindow(window.Properties.NativeWindowHandle, SW_MAXIMIZE);
                SetForegroundWindow(window.Properties.NativeWindowHandle);
                window.Focus();

                var punto = celda.GetClickablePoint();


                FlaUI.Core.Input.Mouse.MoveTo(punto);
                await Task.Delay(100);

                FlaUI.Core.Input.Mouse.Click(FlaUI.Core.Input.MouseButton.Right);
                await Task.Delay(100);

                Console.WriteLine($"✅ Click derecho ejecutado en valor {valorBuscado}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return false;
            }
        }






        
        public static async Task<bool> ClickDerechoEnFilaPorTextoClipboardAsync(
    FlaUIApplication app,
    UIA3Automation automation,
    string valorBuscado,
    int maxFilas = 50)
    {
        try
        {
            var window = app.GetMainWindow(automation);

            // ✅ Asegurar foco real
            ShowWindow(window.Properties.NativeWindowHandle, 3);
            SetForegroundWindow(window.Properties.NativeWindowHandle);
            window.Focus();

            await Task.Delay(500);

            // ✅ Click en la grilla (importante para que CTRL+C funcione)
            var center = window.BoundingRectangle.Center();
            Mouse.MoveTo(center);
            Mouse.Click(MouseButton.Left);

            await Task.Delay(300);

                // ✅ Ir al inicio
                Keyboard.Press(VirtualKeyShort.HOME);
                await Task.Delay(300);

            for (int i = 0; i < maxFilas; i++)
            {
                // ✅ Intentar copiar contenido de la fila actual
                ClearClipboardSTA();

                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_C);

                Keyboard.Release(VirtualKeyShort.KEY_C);
                Keyboard.Release(VirtualKeyShort.CONTROL);

                await Task.Delay(200);

                string texto = GetClipboardTextSTA();

             

                Console.WriteLine($"Fila {i}: [{texto}]");

                // ✅ Validar si encontramos el valor
                if (!string.IsNullOrEmpty(texto) && texto.Contains(valorBuscado))
                {
                    Console.WriteLine($"✅ Encontrado en fila {i}");

                    await Task.Delay(200);

                    // ✅ Abrir menú contextual
                    Keyboard.Press(VirtualKeyShort.SHIFT);
                    Keyboard.Press(VirtualKeyShort.F10);

                    Keyboard.Release(VirtualKeyShort.F10);
                    Keyboard.Release(VirtualKeyShort.SHIFT);


                    await Task.Delay(500);

                    // ✅ Listar opciones
                    //await ImprimirOpcionesMenuAsync(automation);


                    // bajar opciones controladamente
                    Keyboard.Press(VirtualKeyShort.DOWN);
                    await Task.Delay(150);

                    Keyboard.Press(VirtualKeyShort.DOWN);
                    await Task.Delay(150);

                    // seleccionar
                    Keyboard.Press(VirtualKeyShort.ENTER);



                    return true;
                }

                // ✅ Bajar a la siguiente fila
                Keyboard.Press(VirtualKeyShort.DOWN);
                await Task.Delay(150);
            }

            Console.WriteLine("❌ No se encontró el valor en la grilla");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return false;
        }
    }



        public static async Task<bool> IngresarPrioridad90YEnterAsync(
    FlaUIApplication app,
    UIA3Automation automation,
    int timeout = 10000)
        {
            try
            {
                var window = app.GetMainWindow(automation);

                await Task.Delay(3000);

                //var sw = Stopwatch.StartNew();
                //AutomationElement textbox = null;

                //// 🔍 Buscar el textbox
                //while (sw.ElapsedMilliseconds < timeout)
                //{
                //    var elementos = window.FindAllDescendants(cf =>
                //        cf.ByAutomationId("PART_Editor")
                //          .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))
                //    );

                //    foreach (var el in elementos)
                //    {
                //        if (!el.Properties.IsOffscreen.Value)
                //        {
                //            textbox = el;
                //            break;
                //        }
                //    }

                //    if (textbox != null)
                //        break;

                //    await Task.Delay(200);
                //}

                //if (textbox == null)
                //    throw new Exception("No se encontró el textbox PART_Editor");

                //// ✅ Focus
                //textbox.Focus();
                //await Task.Delay(2000);

                //// ✅ Click para asegurar edición real
                //var p = textbox.GetClickablePoint();
                //FlaUI.Core.Input.Mouse.MoveTo(p);
                //FlaUI.Core.Input.Mouse.Click(FlaUI.Core.Input.MouseButton.Left);

                await Task.Delay(150);

                // ✅ Limpiar contenido (CTRL + A)
                Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);
                Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);

                Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);

                await Task.Delay(100);

                // ✅ Escribir "90"
                Keyboard.Type("90");

                await Task.Delay(100);

                // ✅ ENTER (commit)
                // VOLVER A COMENTAR PARA PRUEBAS
                Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);

                Console.WriteLine("✅ Se ingresó 90 y se confirmó con ENTER");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return false;
            }
        }


        public static async Task ImprimirOpcionesMenuAsync(UIA3Automation automation)
        {
            await Task.Delay(300);

            var desktop = automation.GetDesktop();

            var menus = desktop.FindAllDescendants(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu)
            );

            if (menus.Length == 0)
            {
                menus = desktop.FindAllDescendants(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                );
            }

            foreach (var menu in menus)
            {
                try
                {
                    if (menu.Properties.IsOffscreen.Value)
                        continue;

                    Console.WriteLine("📋 MENU DETECTADO:");

                    var items = menu.FindAllDescendants(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem)
                    );

                    foreach (var item in items)
                    {
                        try
                        {
                            var nombre = item.Properties.Name.Value;

                            if (!string.IsNullOrEmpty(nombre))
                            {
                                Console.WriteLine($" - {nombre}");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        public static string GetClipboardTextSTA()
        {
            string result = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                        result = Clipboard.GetText();
                }
                catch { }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return result;
        }

        public static void ClearClipboardSTA()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Clipboard.Clear();
                }
                catch { }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }




        public static async Task<bool> ClickDerechoEnFilaPorTextoClipboardIniciarOleadaAsync( FlaUIApplication app, UIA3Automation automation, string valorBuscado, int maxFilas = 50)
        {
            try
            {
                var window = app.GetMainWindow(automation);

                // ✅ Asegurar foco real
                ShowWindow(window.Properties.NativeWindowHandle, 3);
                SetForegroundWindow(window.Properties.NativeWindowHandle);
                window.Focus();

                await Task.Delay(500);

                // ✅ Click en la grilla (importante para que CTRL+C funcione)
                var center = window.BoundingRectangle.Center();
                Mouse.MoveTo(center);
                Mouse.Click(MouseButton.Left);

                await Task.Delay(300);

                // ✅ Ir al inicio
                Keyboard.Press(VirtualKeyShort.HOME);
                await Task.Delay(300);

                for (int i = 0; i < maxFilas; i++)
                {
                    // ✅ Intentar copiar contenido de la fila actual
                    ClearClipboardSTA();

                    Keyboard.Press(VirtualKeyShort.CONTROL);
                    Keyboard.Press(VirtualKeyShort.KEY_C);

                    Keyboard.Release(VirtualKeyShort.KEY_C);
                    Keyboard.Release(VirtualKeyShort.CONTROL);

                    await Task.Delay(200);

                    string texto = GetClipboardTextSTA();



                    Console.WriteLine($"Fila {i}: [{texto}]");

                    // ✅ Validar si encontramos el valor
                    if (!string.IsNullOrEmpty(texto) && texto.Contains(valorBuscado))
                    {
                        Console.WriteLine($"✅ Encontrado en fila {i}");

                        await Task.Delay(200);

                        // ✅ Abrir menú contextual
                        Keyboard.Press(VirtualKeyShort.SHIFT);
                        Keyboard.Press(VirtualKeyShort.F10);

                        Keyboard.Release(VirtualKeyShort.F10);
                        Keyboard.Release(VirtualKeyShort.SHIFT);


                        await Task.Delay(500);

                        // ✅ Listar opciones
                        //await ImprimirOpcionesMenuAsync(automation);


                        // bajar opciones controladamente
                        Keyboard.Press(VirtualKeyShort.DOWN);
                        await Task.Delay(150);

                        // seleccionar
                        Keyboard.Press(VirtualKeyShort.ENTER);



                        return true;
                    }

                    // ✅ Bajar a la siguiente fila
                    Keyboard.Press(VirtualKeyShort.DOWN);
                    await Task.Delay(150);
                }

                Console.WriteLine("❌ No se encontró el valor en la grilla");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return false;
            }
        }


        public static bool PulsarBotonSi(FlaUIApplication app, UIA3Automation automation, int timeout = 10)
        {
            var end = DateTime.Now.AddSeconds(timeout);

            while (DateTime.Now < end)
            {
                var desktop = automation.GetDesktop();

                var boton = desktop.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName("Sí"))
                )?.AsButton();

                if (boton != null)
                {
                    Console.WriteLine("✅ Botón Sí encontrado");

                    try
                    {
                        if (boton.Patterns.Invoke.IsSupported)
                        {
                            boton.Patterns.Invoke.Pattern.Invoke();
                        }
                        else
                        {
                            boton.Click();
                        }

                        Console.WriteLine("✅ Botón Sí presionado");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error: {ex.Message}");
                    }
                }

                Thread.Sleep(300);
            }

            Console.WriteLine("❌ No se encontró el botón Sí");
            return false;
        }
    }
}
