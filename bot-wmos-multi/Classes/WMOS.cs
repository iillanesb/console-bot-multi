using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WmosAutomatizacion.Classes;

namespace WmosAutomatizacionNew.Classes
{
    class WMOS
    {
        public static async Task<(IPage page, IBrowserContext context)> InitWmosSession(IBrowser browser)
        {
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = null
            });

            var page = await context.NewPageAsync();

            await page.GotoAsync("https://wmslof2.falabella.cl:20001/login.jsp", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });


            await page.Locator("#username").FocusAsync();
            await page.Keyboard.TypeAsync("cl18108679");    // FELIPE
            //await page.Keyboard.TypeAsync("cl19230872");

            await page.Locator("#password").FocusAsync();
            await page.Keyboard.TypeAsync("Tradis26");      //FELIPE
            //await page.Keyboard.TypeAsync("Tradis85.."); 


            await Task.WhenAll(
                page.WaitForURLAsync("**"),
                page.ClickAsync("#loginButton")
            );


            var currentUrl = page.Url;
            if (currentUrl.Contains(":20001"))
            {
                var fixedUrl = currentUrl.Replace(":20001", "");
                await page.GotoAsync(fixedUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            }

            await page.WaitForURLAsync("**/manh/index.html?i=83");


            return (page, context);
        }

        public static async Task ClickEnCustomerOrderSingle(IPage page)
        {
            var targetText = "Customer Order Single";
            //var cellHandle = await ExtJsHelpers.RetryFindCellAsync(page, targetText, 8, 600);
            var cellHandle = await ExtJsHelpers.RetryFindCellExactAsync(page, targetText, 8, 600);

            if (cellHandle is null)
            {
                await page.ScreenshotAsync(new() { Path = "td_not_found.png", FullPage = true });
                throw new Exception($"No se encontró el TD con el texto '{targetText}'");
            }

            await cellHandle.ScrollIntoViewIfNeededAsync();
            await cellHandle.DblClickAsync();
        }

        public static async Task ClickEnCustomerOrderMulti(IPage page)
        {
            var targetText = "Customer Order Multi";
            //var cellHandle = await ExtJsHelpers.RetryFindCellAsync(page, targetText, 8, 600);
            var cellHandle = await ExtJsHelpers.RetryFindCellExactAsync(page, targetText, 8, 600);

            if (cellHandle is null)
            {
                await page.ScreenshotAsync(new() { Path = "td_not_found.png", FullPage = true });
                throw new Exception($"No se encontró el TD con el texto '{targetText}'");
            }

            await cellHandle.ScrollIntoViewIfNeededAsync();
            await cellHandle.DblClickAsync();
        }

        public static async Task ClickEnSDDSingle(IPage page)
        {
            await ExtJsHelpers.ClickSpanAndNextInputAsync(page, exactText: "SDD Single");
        }

        public static async Task ClickEnSDDMulti(IPage page)
        {
            await ExtJsHelpers.ClickSpanAndNextInputAsync(page, exactText: "SDD Multi");
        }

        private static TimeZoneInfo GetChileTimeZoneOrLocal()
        {
            // IANA (Linux/macOS) vs Windows ID
            var ianaId = "America/Santiago";
            var windowsId = "Pacific SA Standard Time";

            string tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? windowsId : ianaId;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch
            {
                Console.WriteLine($"[WARN] No se encontró la zona horaria '{tzId}' en este sistema. Usando TimeZoneInfo.Local.");
                return TimeZoneInfo.Local;
            }
        }

        public static bool DebeUsarWaveNew()
        {
            var chileTz = GetChileTimeZoneOrLocal();
            var ahoraChile = TimeZoneInfo.ConvertTime(DateTime.UtcNow, chileTz).TimeOfDay;

            var inicio = new TimeSpan(13, 30, 0); // 13:30
            var fin = new TimeSpan(15, 0, 0); // 15:00

            // Rango inclusivo: [13:30, 15:00]
            return ahoraChile >= inicio && ahoraChile <= fin;
        }

        public class WaveDetails
        {
            public string WAVE { get; set; }
            public string STATUS { get; set; }
            public string ALLOCATED { get; set; }
            public string LPNS { get; set; }
            public int CAJAS { get; set; }
        }

        public class WAVEWMOS
        {
            public int NBR_OF_DETAIL { get; set; } = 0;
            //public string TASK_ID { get; set; } = string.Empty;
            //public string AREA_ACTUAL { get; set; } = string.Empty;
            //public string TASK_DESC { get; set; } = string.Empty;
            //public string WAVE { get; set; } = string.Empty;
            //public string WAVE_DESC { get; set; } = string.Empty;
            //public string DESCRIPC_WAVE { get; set; } = string.Empty;
            //public string ILPN { get; set; } = string.Empty;
            //public string END_CURR_WORK_GRP { get; set; } = string.Empty;
            //public string END_CURR_WORK_AREA { get; set; } = string.Empty;
            //public string CREATE_DATE_TIME { get; set; } = string.Empty;
            //public string HORA_CREAC { get; set; } = string.Empty;
            //public string MOD_DATE_TIME { get; set; } = string.Empty;
            //public string HORA_MODIF { get; set; } = string.Empty;
            //public string USER_ID { get; set; } = string.Empty;
            //public string NOMBRE { get; set; } = string.Empty;
            //public string ITEM_NAME { get; set; } = string.Empty;
            //public string PULL_LOCN_DSP { get; set; } = string.Empty;
            //public string DEST_LOCN_DSP { get; set; } = string.Empty;
        }

    }
}
