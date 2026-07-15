using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.Linq;
using System.Threading;

namespace ConsoleKisoft
{
    internal class Helpers
    {
        private static void IdentificarVentanas(Application app, UIA3Automation automation)
        {
            // Ignora la ventana de login (ya la conoces) y busca una nueva
            var ventanaKisof = app.GetAllTopLevelWindows(automation)
                                    .ToList();

            foreach (var nueva in ventanaKisof)
            {
                Console.WriteLine("Nueva ventana: " + nueva.Title);
            }
        }

        private static void VerJerarquiaHijos(Window ventana)
        {
            Thread.Sleep(2000);
            var hijos = ventana.FindAllChildren();
            Console.WriteLine($"Total hijos directos: {hijos.Length}");
            for (int i = 0; i < hijos.Length; i++)
            {
                Console.WriteLine($"[{i}] Tipo: {hijos[i].ControlType}, Nombre: {hijos[i].Name}");
                hijos[i].DrawHighlight(); // resalta visualmente
                Thread.Sleep(1000); // para que veas cada uno
            }
        }

        private static void VerJerarquiaProfunda(Window ventana)
        {
            Thread.Sleep(2000);
            var descendientes = ventana.FindAllDescendants();
            Console.WriteLine($"🔍 Total descendientes: {descendientes.Length}");
            for (int i = 0; i < descendientes.Length; i++)
            {
                Console.WriteLine($"[{i}] Tipo: {descendientes[i].ControlType}, Nombre: {descendientes[i].Name}, ID: {descendientes[i].AutomationId}");
                descendientes[i].DrawHighlight();
                Thread.Sleep(500); // más rápido
            }
        }

        private static void ListarTodosLosBotones(Window ventana)
        {
            var botones = ventana.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            Console.WriteLine($"🔍 Botones encontrados: {botones.Length}");

            for (int i = 0; i < botones.Length; i++)
            {
                Console.WriteLine($"[{i}] Nombre: {botones[i].Name} / ID: {botones[i].AutomationId}");
                Thread.Sleep(500); // para que puedas ver cada uno
            }
        }
    }
}
