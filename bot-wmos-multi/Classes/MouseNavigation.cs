using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WmosAutomatizacion
{
    internal class MouseNavigation
    {
        // necesario para la navegación del mouse
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private struct POINT
        {
            public int X;
            public int Y;
        }

        public static async Task ClickEnPosicion(int x, int y)
        {
            SetCursorPos(x, y);
            await Task.Delay(2000); // espera sin bloquear
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
            await Task.Delay(2000); // espera sin bloquear
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
        }

        public static async Task DobleClickEnPosicion(int x, int y)
        {
            SetCursorPos(x, y);
            await Task.Delay(1000); // pequeña pausa antes del primer clic

            // Primer clic
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);

            await Task.Delay(150); // intervalo entre clics (importante para que se detecte como doble clic)

            // Segundo clic
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
        }


        public static void ClickConShift(Point posicion)
        {
            // Presionar Shift
            Keyboard.Press(VirtualKeyShort.SHIFT);

            // Hacer click en la posición deseada
            Mouse.MoveTo(posicion);
            Mouse.Click(MouseButton.Left);

            // Espera opcional 
            Thread.Sleep(100);

            // Soltar Shift
            Keyboard.Release(VirtualKeyShort.SHIFT);
        }

        public static void ArrastrarSuavemente(Point inicio, Point fin, int pasos = 20, int delayMs = 10)
        {
            // Mover al punto inicial
            Mouse.MoveTo(inicio);
            Thread.Sleep(500);

            // Presionar botón izquierdo
            Mouse.Down(MouseButton.Left);

            // Interpolar movimiento en pequeños pasos
            for (int i = 1; i <= pasos; i++)
            {
                var x = inicio.X + (fin.X - inicio.X) * i / pasos;
                var y = inicio.Y + (fin.Y - inicio.Y) * i / pasos;
                Mouse.MoveTo(new Point(x, y));
                Thread.Sleep(delayMs); // pausa entre pasos
            }

            // Soltar botón
            Thread.Sleep(500); // opcional, para que se note la "retención"
            Mouse.Up(MouseButton.Left);
        }

        public static void ArrastrarClickMouse(Point inicio, Point fin)
        {
            //Console.WriteLine($"Arrastrando desde {inicio} hasta {fin}");

            // Mueve el mouse al punto inicial
            Mouse.MoveTo(inicio);
            Thread.Sleep(1000); // Espera para asegurar la posición

            // Mantiene presionado el botón izquierdo del mouse
            Mouse.Down(MouseButton.Left);
            Thread.Sleep(1000); // Espera un poco para simular acción humana

            // Mueve hasta el punto final
            Mouse.MoveTo(fin);
            Thread.Sleep(1000);

            // Suelta el botón del mouse
            Mouse.Up(MouseButton.Left);
            Thread.Sleep(1000);
        }

        public static void MostrarPosicionMouse()
        {
            Console.WriteLine("📍 Presiona ESC para detener.");
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    break;

                GetCursorPos(out POINT punto);
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"🖱 Coordenadas: X = {punto.X}, Y = {punto.Y}     ");
                Thread.Sleep(100);
            }

            Console.WriteLine("\n✅ Lectura de coordenadas finalizada.");
        }
    }
}
