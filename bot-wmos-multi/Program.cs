using Api.Clases;
using FlaUI.UIA3;
using Microsoft.Playwright;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WmosAutomatizacion.Flujos;
using WmosAutomatizacion.Helpers;
using WmosAutomatizacionNew.Classes;
using WmosAutomatizacionNew.Helpers;
using static WmosAutomatizacionNew.Classes.WMOS;
using FlaUIApplication = FlaUI.Core.Application;    // Alias para FlaUI


namespace WmosAutomatizacion.Classes
{
    class Program
    {
        private const string AppName = "ConsoleKisoft";
        private const string AppPath = @"C:\KisoftN\KisoftN.exe";
        private const string AppNameKisoft = "KisoftN";
        private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(45);

        public static async Task Main()
        {

            FlaUIApplication app = null;
            UIA3Automation automation = null;
            var logger = new LoggerService();
            
            try
            {

                logger.Log("Inicio aplicación");

                using var pw = await Playwright.CreateAsync();
                logger.Log("Playwright inicializado");

                await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    Args = new[] { "--start-maximized", "--ignore-certificate-errors", "--allow-insecure-localhost" }
                });
                logger.Log("Browser iniciado");

                // LOGIN
                var (page, context) = await WMOS.InitWmosSession(browser);
                logger.Log("Sesión WMOS iniciada");



                // FLUJO PUTWALL 1
                await ExtJsHelpers.DblClickLabelByTextAsync(page, "Pack Wave Par...");
                logger.Log("Click Pack Wave Par...");
                //await ExtJsHelpers.ClickTabByTitleAsync(page, "Sorter Groups");
                await ExtJsHelpers.ClickTabAsync( page, "Sorter Groups");
                logger.Log("Click Sorter Groups");


                await ExtJsHelpers.ClickRowByHeaderValueAsync( page, "dataForm:listView:dataTable", "Description", "Putwall");
                logger.Log("Click Putwall");

                await ExtJsHelpers.ClickInputByValueAsync( page, "Edit", allowSuffix: false, timeoutMs: 10000);
                logger.Log("Click Edit");

                await ExtJsHelpers.ClickInputByValueAsync( page, "<< Remove All", allowSuffix: false, timeoutMs: 10000);
                logger.Log("Click << Remove All");

                //await ExtJsHelpers.SelectOptionAndClickAddAsync(page, "PTW1");
                await ExtJsHelpers.SelectPTW1AndClickAddAsync( page);
                logger.Log("Click ADD PTW1");


                await ExtJsHelpers.ClickInputByValueAsync(page, "Save", allowSuffix: false, timeoutMs: 10000);
                logger.Log("Click Save");

                // Cerrar ventana
                await Task.Delay(3000);
                await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-close");
                logger.Log("Cierra ventana");
                //
                //
                // Flujo:           Pack Wave Parameters
                // En:              Putwall
                // Seleccionar:     PTW1
                //
                //



                // Empezar flujo para MULTI

                await ExtJsHelpers.DblClickLabelByTextAsync(page, "Run Waves");
                logger.Log("Click Run Waves");


                await WMOS.ClickEnCustomerOrderMulti(page);
                logger.Log("Click Customer Order Multi");

                await WMOS.ClickEnSDDMulti(page);
                logger.Log("Click SDD Multi");

                await ExtJsHelpers.ClickLinkByTextAsync(page, "Parameters");
                logger.Log("Click Parameters");


                // Preguntar si sigue misma lógica que en single
                if (WMOS.DebeUsarWaveNew())
                {
                    await ExtJsHelpers.SelectOptionByTrimmedTextAsync(
                        page,
                        "dataForm:dependentListAllocType",
                        "Wave Customer Order New"
                    );
                    logger.Log("Selecciona Wave Customer Order New");
                }
                else
                {
                    await ExtJsHelpers.SelectOptionByTrimmedTextAsync(
                        page,
                        "dataForm:dependentListAllocType",
                        "Wave Customer Order"
                    );
                    logger.Log("Selecciona Wave Customer Order");
                }
                // Fin preguntar si es misma lógica que single

                await ExtJsHelpers.ClickInputByValueAsync(page, "Submit", allowSuffix: true);
                logger.Log("Click Submit y espera 6 segundos");

                // AQUI EL NUEVO FLUJO
                // SE DEBE PREGUNTAR CON LA QUERY
                // POR LAS MINI OLAS DEL MULTI PARA REVISAR EN KISOFT

                await Task.Delay(6000);

                var waveNumber = await ExtJsHelpers.DblClickAnchorAndGetTextAsync(
                    page,
                    anchorId: "dataForm:AwvNbrRun"
                );
                logger.Log($"Wave obtenida: {waveNumber}");
                logger.ActualizarNombreConWave(waveNumber);

                var status = await ExtJsHelpers.GetRow0FifthTdStatusAndDblClickAsync(page);
                logger.Log("Obtención de status"); 
                logger.Log($"Status obtenido: {status}");

                var unitsAllocated = await ExtJsHelpers.GetCaptionIntAsync(page, "dataForm:UnitsAllocated");
                logger.Log("Obtención UnitsAllocated"); 
                logger.Log($"UnitsAllocated: {unitsAllocated}");

                var lpns = await ExtJsHelpers.GetCaptionIntAsync(page, "dataForm:Lpns");
                logger.Log("Obtención LPNS"); 
                logger.Log($"LPNS: {lpns}");

                //waveNumber = "202601290090";
                //waveNumber = "202601300036";
                //var queryUsar = @$"SELECT TASK_HDR.TASK_ID, LOCN_HDR.AREA AS AREA_ACTUAL, TASK_HDR.TASK_DESC, SHIP_WAVE_PARM.SHIP_WAVE_NBR AS WAVE, SHIP_WAVE_PARM.WAVE_DESC, WAVE_PARM.WAVE_DESC AS DESCRIPC_WAVE, TASK_DTL.CNTR_NBR AS ILPN, TASK_HDR.END_CURR_WORK_GRP, TASK_HDR.END_CURR_WORK_AREA, TASK_HDR.CREATE_DATE_TIME, TO_CHAR(TASK_HDR.create_date_time, 'HH24:MI:SS') AS HORA_CREAC, TASK_HDR.MOD_DATE_TIME, TO_CHAR(TASK_HDR.MOD_DATE_TIME, 'HH24:MI:SS') AS HORA_MODIF, TASK_DTL.USER_ID, concat(ucl_user.user_first_name, ucl_user.user_last_name) as Nombre, LPN.ITEM_NAME, lh1.DSP_LOCN AS PULL_LOCN_DSP, lh2.DSP_LOCN AS DEST_LOCN_DSP FROM TASK_HDR INNER JOIN TASK_DTL ON TASK_DTL.TASK_ID = TASK_HDR.TASK_ID INNER JOIN LPN ON LPN.TC_LPN_ID = TASK_DTL.CNTR_NBR INNER JOIN SHIP_WAVE_PARM ON SHIP_WAVE_PARM.SHIP_WAVE_NBR = TASK_HDR.TASK_GENRTN_REF_NBR LEFT JOIN LOCN_HDR ON locn_hdr.locn_id = LPN.CURR_SUB_LOCN_ID LEFT JOIN LOCN_HDR lh1 ON TASK_DTL.PULL_LOCN_ID = lh1.LOCN_ID LEFT JOIN LOCN_HDR lh2 ON TASK_DTL.DEST_LOCN_ID = lh2.LOCN_ID LEFT JOIN WAVE_PARM ON SHIP_WAVE_PARM.SHIP_WAVE_NBR = WAVE_PARM.WAVE_NBR left join ucl_user on ucl_user.user_name = task_dtl.user_id WHERE SUBSTR(TASK_HDR.TASK_GENRTN_REF_NBR,1,4)in ('2025', '2026') AND LOCN_HDR.AREA IN('RCK','DZO','IND','DZG','ISL','SOR') AND task_dtl.stat_code NOT IN('90', '99') AND SHIP_WAVE_PARM.SHIP_WAVE_NBR = '{waveNumber}' ORDER BY TASK_HDR.TASK_GENRTN_REF_NBR DESC";



                await Task.Delay(3000);

                logger.Log("Consulta Oracle inicio");
                // Se agrega type 10 02-07-2026
                var queryUsar = $@"SELECT case when cntr_nbr is null then 0 else 1 end as NBR_OF_DETAIL FROM  task_hdr hdr left JOIN task_dtl dtl ON hdr.task_id = dtl.task_id left join sys_code on sys_code.code_id = hdr.stat_code and sys_code.code_type in ('552') WHERE  1 = 1 AND hdr.task_cmpl_ref_nbr IN ('{waveNumber}') AND hdr.invn_need_type IN ('1','2', '10') AND sys_code.code_desc in ('Released')";

                List<WAVEWMOS> waveWmos = new List<WAVEWMOS>();
                waveWmos = await Oracle.ConsultaOracleDatasetAsync<WAVEWMOS>(queryUsar, "WMS_LOF2_WF");

                if ((waveWmos != null) && (waveWmos.Sum(x => x.NBR_OF_DETAIL) > 0))
                {
                    logger.Log($"Cajas encontradas: {waveWmos.Sum(x => x.NBR_OF_DETAIL)}");
                }
                else
                {
                    logger.Log($"Sin cajas encontradas.");
                }

                //if (released.HasValue)
                //if(waveWmos.Count > 0)
                //{
                // Avisar por teams
                WaveDetails wave = new WaveDetails()
                {
                    WAVE = waveNumber,
                    STATUS = status,
                    ALLOCATED = unitsAllocated.ToString(),
                    LPNS = lpns.ToString(),
                    CAJAS = waveWmos.Count > 0 ? waveWmos.Sum(x => x.NBR_OF_DETAIL) : 0
                };
                logger.Log("Crea objeto a enviar a teams");

                //WaveDetails wave = new WaveDetails()
                //{
                //    WAVE = "123",
                //    STATUS = "status",
                //    ALLOCATED = "0",
                //    LPNS = "0",
                //    CAJAS = 0
                //};
                Teams.enviarMensajeTeams(wave);
                logger.Log("Envia mensaje por MariaBot Teams");
                //}


                //
                // PRUEBAS, COMENTAR LUEGO
                //
                //
                        var waveTest = "123";
                        GuardarWave(waveTest);
                //
                //
                //

                GuardarWave(waveNumber);


                // Flujo MULTI llega hasta aqui...


                //// TASKS
                //await Task.Delay(3000);
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-close");
                //logger.Log("Cierra ventana");

                //await Task.Delay(3000);
                //await ExtJsHelpers.DblClickLabelByTextAsync(page, "Tasks");
                //logger.Log("Click en Tasks");

                //await Task.Delay(4000);
                //// requiere esperar un momento o carga mal
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-maximize");
                //logger.Log("Maximiza ventana");

                //await Task.Delay(3000);
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-restore");
                //logger.Log("Restaura ventana");

                //await Task.Delay(3000);
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-maximize");
                //logger.Log("Maximiza ventana");
                //await Task.Delay(3000);


                //await ExtJsHelpers.SelectOptionByCaptionAsync(
                //    page,
                //    captionText: "Header Status",
                //    optionVisibleText: "Locked/Disabled",
                //    allowContainsFallback: false,
                //    timeoutMs: 10000
                //);
                //logger.Log("Header Status lo deja en Locked / Disabled");

                //await Task.Delay(3000);
                //await ExtJsHelpers.ClickInputByValueAsync(page, "Apply", allowSuffix: false);
                //logger.Log("Click botón Apply");

       
                //await Task.Delay(3000);
                //await ExtJsHelpers.SelectAllRowsOfficiallyAndVisuallyAsync(
                //    page,
                //    tableIdPrefix: "dataForm:lview:dataTable",
                //    headerText: "Task Generation Reference Number",
                //    targetValue: waveNumber,  // luego reemplaza por waveNumber
                //    exact: true,
                //    timeoutMs: 12000
                //);
                //logger.Log("Selecciona todas las rows asociadas al WaveNumber - Task Generation Reference Number");


                //await Task.Delay(3000);
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-restore");
                //logger.Log("Restaura ventana");

         

                //await Task.Delay(3000);
                //var dialogMsg = await ExtJsHelpers.ClickInputByValueUntilDialogAndAcceptAsync(
                //    page,
                //    visibleValue: "Release Task",
                //    totalTimeoutMs: 30000,     // ajusta 
                //    minIntervalMs: 250,
                //    maxIntervalMs: 1000,
                //    ensureEnabledBeforeFirstClick: true,
                //    clickMode: ExtJsHelpers.ClickMode.Double //  fuerza doble click real
                //);
                //logger.Log("Click Release Task");




                //await Task.Delay(3000);
                //// Cerrar la respuesta del alert
                //// Cerrar Tasks
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-close");
                //logger.Log("Cierra ventana");


                ////No es necesario este segundo, con uno se cierra bien!
                ////await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-close");

                //await Task.Delay(3000);
                //// Ultima parte del flujo WMS
                //// Click en waves y luego en single boton derecho
                //await ExtJsHelpers.DblClickLabelByTextAsync(page, "Waves");
                //logger.Log("Click en Waves");

                //await Task.Delay(3000);
                //// Usado para que desaparezca el menu "Remove"
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-maximize");
                //logger.Log("Maximiza ventana");

                //await Task.Delay(3000);
                //await ExtJsHelpers.ClickByCssAsync(page, "img.x-tool-img.x-tool-restore");
                //logger.Log("Restaura ventana");

                //await Task.Delay(3000);
                ////var waveNumberTest = "202602130059";
                //await ExtJsHelpers.ClickRowCheckboxByHeaderValueAsync(
                //    page,
                //    tableIdPrefix: "dataForm:listView:dataTable",
                //    headerText: "Wave Number",
                //    targetValue: waveNumber,
                //    exact: true,
                //    timeoutMs: 12000
                //);
                //logger.Log("Click Checkbox por waveNumber");

                //await Task.Delay(3000);
                //await ExtJsHelpers.RightClickRowByHeaderValueAsync(
                //    page,
                //    "dataForm:listView:dataTable",
                //    "Wave Number",
                //    waveNumber,
                //    exact: true
                //);
                //logger.Log("Botón derecho en waveNumber");

                //await Task.Delay(3000);
                //var dialogMsgRelease = await ExtJsHelpers.ClickContextMenuItemUntilDialogAndAcceptAsync(
                //    page,
                //    menuText: "Release MHE Messages",
                //    totalTimeoutMs: 30000,
                //    minIntervalMs: 250,
                //    maxIntervalMs: 1200,
                //    clickMode: ExtJsHelpers.ClickMode.Both,  // Single + Double en cada iteración
                //    allowContainsFallback: false
                ////reopenMenuAsync: reopenMenu              // o null si el menú no se cierra
                //);
                //logger.Log("Click Release MHE Messages");




                // abrir kisoft

                logger.Log("Cerrando navegador");

                context.SetDefaultTimeout(1000000);
                await browser.CloseAsync();
                pw.Dispose();


                logger.Log("Fin Flujo WMOS");
                logger.Log("Inicio Flujo Kisoft");



                List<WAVE_DTO> listadoWavesNoProcesadas = new List<WAVE_DTO>();
                listadoWavesNoProcesadas = ObtenerWavesNoProcesadas();


                foreach (var waveItem in listadoWavesNoProcesadas)
                {
                    string queryBase = @"SELECT EM.EK_WAVE_NBR WAVE_KISOFT, EM.EVENT_MESSAGE_ID WMOS_MSG, EM.CL_MESSAGE_ID KNAPP_MSG, EM.EVENT_ID, CMS.STATUS_NAME STATUS, EM.create_date_time EV_DTTM, TD.SHIP_WAVE_NBR WMOS_WAVE, SWP.WAVE_DESC, (WTASK.SUM_COMPLETED / WTASK.SUM_TASKS) * 100 COMPLETED, WTASK.SUM_TASKS INT1, WTASK.SUM_COMPLETED INT1_CONSUMED, EM.EK_WAVE_NBR, TASK_SUMMARY.SUM_TOTAL TAREAS_TOTAL, TASK_SUMMARY.SUM_TOTAL_MZ ""TAREAS_MZN (TOTAL)"", TASK_SUMMARY.SUM_RELEASED_MZ ""TAREAS_MZN (RELEASED)"", TASK_SUMMARY.SUM_LOCK_MZ ""TAREAS_MZN (LOCKED)"", TASK_SUMMARY.SUM_TOTAL_GT ""TAREAS_GTP (TOTAL)"", TASK_SUMMARY.SUM_RELEASED_GT ""TAREAS_GTP (RELEASED)"", TASK_SUMMARY.SUM_LOCK_GT ""TAREAS_GTP (LOCKED)"" FROM event_message EM INNER JOIN (SELECT DISTINCT SWP.SHIP_WAVE_NBR, TD.TASK_GENRTN_REF_NBR FROM TASK_DTL TD INNER JOIN LPN L ON L.TC_LPN_ID = TD.CARTON_NBR INNER JOIN SHIP_WAVE_PARM SWP ON SWP.SHIP_WAVE_NBR = L.WAVE_NBR WHERE TD.TASK_GENRTN_REF_CODE = '44') TD ON TD.TASK_GENRTN_REF_NBR = EM.EK_WAVE_NBR INNER JOIN SHIP_WAVE_PARM SWP ON SWP.SHIP_WAVE_NBR = TD.SHIP_WAVE_NBR LEFT JOIN (SELECT TD_TODAS.SHIP_WAVE_NBR, TD_TODAS.SUM_TASKS, TD_90.SUM_COMPLETED, TD_0.SUM_RELEASED, TD_40.SUM_INPROGRESS FROM (SELECT WAVES.SHIP_WAVE_NBR, COUNT(TD.TASK_ID) SUM_TASKS FROM TASK_DTL TD INNER JOIN (SELECT * FROM SHIP_WAVE_PARM SWP WHERE SWP.WAVE_DESC LIKE 'Chase Wave' OR SWP.WAVE_DESC LIKE 'Customer Order Multi%') WAVES ON TD.TASK_GENRTN_REF_NBR = WAVES.SHIP_WAVE_NBR WHERE TD.INVN_NEED_TYPE = '1' AND TD.STAT_CODE NOT IN ('99') GROUP BY WAVES.SHIP_WAVE_NBR) TD_TODAS LEFT JOIN (SELECT WAVES.SHIP_WAVE_NBR, COUNT(TD.TASK_ID) SUM_RELEASED FROM TASK_DTL TD INNER JOIN (SELECT * FROM SHIP_WAVE_PARM SWP WHERE SWP.WAVE_DESC LIKE 'Chase Wave' OR SWP.WAVE_DESC LIKE 'Customer Order Multi%') WAVES ON TD.TASK_GENRTN_REF_NBR = WAVES.SHIP_WAVE_NBR WHERE TD.INVN_NEED_TYPE = '1' AND TD.STAT_CODE IN ('0') GROUP BY WAVES.SHIP_WAVE_NBR) TD_0 ON TD_TODAS.SHIP_WAVE_NBR = TD_0.SHIP_WAVE_NBR LEFT JOIN (SELECT WAVES.SHIP_WAVE_NBR, COUNT(TD.TASK_ID) SUM_COMPLETED FROM TASK_DTL TD INNER JOIN (SELECT * FROM SHIP_WAVE_PARM SWP WHERE SWP.WAVE_DESC LIKE 'Chase Wave' OR SWP.WAVE_DESC LIKE 'Customer Order Multi%') WAVES ON TD.TASK_GENRTN_REF_NBR = WAVES.SHIP_WAVE_NBR WHERE TD.INVN_NEED_TYPE = '1' AND TD.STAT_CODE IN ('90') GROUP BY WAVES.SHIP_WAVE_NBR) TD_90 ON TD_TODAS.SHIP_WAVE_NBR = TD_90.SHIP_WAVE_NBR LEFT JOIN (SELECT WAVES.SHIP_WAVE_NBR, COUNT(TD.TASK_ID) SUM_INPROGRESS FROM TASK_DTL TD INNER JOIN (SELECT * FROM SHIP_WAVE_PARM SWP WHERE SWP.WAVE_DESC LIKE 'Chase Wave' OR SWP.WAVE_DESC LIKE 'Customer Order Multi%') WAVES ON TD.TASK_GENRTN_REF_NBR = WAVES.SHIP_WAVE_NBR WHERE TD.INVN_NEED_TYPE = '1' AND TD.STAT_CODE IN ('40') GROUP BY WAVES.SHIP_WAVE_NBR) TD_40 ON TD_TODAS.SHIP_WAVE_NBR = TD_40.SHIP_WAVE_NBR) WTASK ON SWP.SHIP_WAVE_NBR = WTASK.SHIP_WAVE_NBR LEFT JOIN (SELECT TOTAL.TASK_GENRTN_REF_NBR PackWave, TOTAL.SUM_TOTAL, TOTALMZ.SUM_TOTAL_MZ, RELEASEDMZ.SUM_RELEASED_MZ, LOCKMZ.SUM_LOCK_MZ, TOTALGT.SUM_TOTAL_GT, RELEASEDGT.SUM_RELEASED_GT, LOCKGT.SUM_LOCK_GT FROM ((SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_TOTAL FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID AND TT.TASK_GENRTN_REF_CODE = '44' GROUP BY TT.TASK_GENRTN_REF_NBR) TOTAL LEFT JOIN (SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_TOTAL_MZ FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID AND TT.TASK_GENRTN_REF_CODE = '44' AND LH.AREA = 'MZ' GROUP BY TT.TASK_GENRTN_REF_NBR) TOTALMZ ON TOTAL.TASK_GENRTN_REF_NBR = TOTALMZ.TASK_GENRTN_REF_NBR LEFT JOIN (SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_RELEASED_MZ FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID INNER JOIN TASK_HDR TH ON TH.TASK_ID = TT.TASK_ID WHERE TH.STAT_CODE IN ('10') AND TT.TASK_GENRTN_REF_CODE = '44' AND LH.AREA = 'MZ' GROUP BY TT.TASK_GENRTN_REF_NBR) RELEASEDMZ ON TOTAL.TASK_GENRTN_REF_NBR = RELEASEDMZ.TASK_GENRTN_REF_NBR LEFT JOIN (SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_LOCK_MZ FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID INNER JOIN TASK_HDR TH ON TH.TASK_ID = TT.TASK_ID WHERE TH.STAT_CODE IN ('5') AND TT.TASK_GENRTN_REF_CODE = '44' AND LH.AREA = 'MZ' GROUP BY TT.TASK_GENRTN_REF_NBR) LOCKMZ ON TOTAL.TASK_GENRTN_REF_NBR = LOCKMZ.TASK_GENRTN_REF_NBR LEFT JOIN (SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_TOTAL_GT FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID AND TT.TASK_GENRTN_REF_CODE = '44' AND LH.AREA = 'GT' GROUP BY TT.TASK_GENRTN_REF_NBR) TOTALGT ON TOTAL.TASK_GENRTN_REF_NBR = TOTALGT.TASK_GENRTN_REF_NBR LEFT JOIN (SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_RELEASED_GT FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID INNER JOIN TASK_HDR TH ON TH.TASK_ID = TT.TASK_ID WHERE TH.STAT_CODE IN ('10') AND TT.TASK_GENRTN_REF_CODE = '44' AND LH.AREA = 'GT' GROUP BY TT.TASK_GENRTN_REF_NBR) RELEASEDGT ON TOTAL.TASK_GENRTN_REF_NBR = RELEASEDGT.TASK_GENRTN_REF_NBR LEFT JOIN (SELECT TT.TASK_GENRTN_REF_NBR, COUNT(TT.TASK_ID) SUM_LOCK_GT FROM TASK_DTL TT INNER JOIN LOCN_HDR LH ON LH.LOCN_ID = TT.PULL_LOCN_ID INNER JOIN TASK_HDR TH ON TH.TASK_ID = TT.TASK_ID WHERE TH.STAT_CODE = '5' AND TT.TASK_GENRTN_REF_CODE = '44' AND LH.AREA = 'GT' GROUP BY TT.TASK_GENRTN_REF_NBR) LOCKGT ON TOTAL.TASK_GENRTN_REF_NBR = LOCKGT.TASK_GENRTN_REF_NBR)) TASK_SUMMARY ON TASK_SUMMARY.PACKWAVE = EM.EK_WAVE_NBR INNER JOIN WMFADMCLPR.CL_ENDPOINT_QUEUE CEQ ON CEQ.MSG_ID = EM.CL_MESSAGE_ID INNER JOIN WMFADMCLPR.CL_MESSAGE_STATUS CMS ON CMS.STATUS_ID = CEQ.STATUS WHERE EM.EVENT_ID = '9032' AND TD.SHIP_WAVE_NBR IN ('{WAVE}') ORDER BY EM.CREATE_DATE_TIME DESC";
                    string queryMiniWaves = queryBase.Replace("{WAVE}", waveItem.WAVE);


                    List<MINIWAVE> miniWave = new List<MINIWAVE>();
                    miniWave = await Oracle.ConsultaOracleDatasetAsync<MINIWAVE>(queryMiniWaves, "WMS_LOF2_WF");

                    if ((miniWave != null) && (miniWave.Count() > 0))
                    {
                        // INICIO FLUJO KISOFT
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = AppPath,
                            Arguments = "--start-maximized"
                        };

                        app = FlaUIApplication.Launch(startInfo);
                        automation = new UIA3Automation();
                        var ventana = app.GetMainWindow(automation);

                        await Login.FlujoLogin(app, automation);
                        logger.Log("Login Kisoft OK");

                        Kisoft.AceptarAdvertenciaSeguro(app, automation);  // Funciona rapido, no modificar, me costo ene jaja
                        logger.Log("Advertencia aceptada");

                        // MODULO OLEADAS (Por coordenadas):
                        Console.WriteLine("Inicio Modulo Oleadas de pedidos");
                        //await Kisoft.ClickPorCoordenadasSeguro(app, automation, 236, 264);

                        //await Kisoft.ClickPorCoordenadasSeguro(app, automation, 236, 264); // Para resolucion en noteboook local
                        await Kisoft.ClickPorCoordenadasSeguro(app, automation, 347, 303); // Para resolución 1080p en el server
                        logger.Log("Módulo Waves abierto");

                        await Kisoft.CambiarValorTextboxPorValorActualAsync(app, automation, "70", "20");
                        logger.Log("Cambiar valor al filtro de 70 a 20");

                        //var wavePrueba = "202606300095";//"42553432";//"202606260073";//"42489049";//"42488256";//"42485116";

                        // Boton derecho y seleccionar Ingresar Prioridad
                        await Kisoft.ClickDerechoEnFilaPorTextoClipboardAsync(app, automation, waveNumber);
                        logger.Log("Buscar en la grilla la wave");

                        // Si retorna false, se debe cortar el flujo
                        // Escribir prioridad 90
                        await Kisoft.IngresarPrioridad90YEnterAsync(app, automation);  // DESCOMENTAR EL ENTER DENTRO, PARA QUE PRESIONE EL OK Y CAMBIE PRIORIDAD
                        logger.Log("Cambia prioridad a 90 de la wave");

                        // INICIAR OLEADA
                        await Kisoft.ClickDerechoEnFilaPorTextoClipboardIniciarOleadaAsync(app, automation, waveNumber);
                        logger.Log("Ininicar Oleada");

                        Kisoft.PulsarBotonSi(app, automation);
                        logger.Log("Botón Sí presionado");


                        // ACTUALIZAR WAVE
                        string miniwavesTexto = string.Join(",", miniWave.Select(x => x.WAVE_KISOFT));
                        ActualizarWave(waveItem.WAVE, miniwavesTexto);

                        logger.Log($"Wave actualizada: {waveItem.WAVE}");
                        logger.Log($"Mini Waves: {miniwavesTexto}");
                        logger.FinalizarOk();


                    }
                }
                    

                }
            catch (Exception ex)
            {
                logger.FinalizarError(ex);
                throw;
            }
            finally
            {
                Kisoft.CerrarAplicacion(app, automation, "Correcto");
            }


            // FIN FLUJO KISOFT
        }

        public static void GuardarWave(string wave)
        {
            try
            {
                var ambiente = Environment.GetEnvironmentVariable("AMBIENTE_AUTOMATIZACION")
                                                ?? ConfigurationManager.AppSettings["Ambiente_Automatizacion"]
                                                ?? "DEV";

                string Get(string key) =>
                Environment.GetEnvironmentVariable(key.Replace(":", "__"))
                                                ?? ConfigurationManager.AppSettings[key];

                
                var url = ambiente switch
                {
                    "LOCAL_AUT" => Get("Urls:LOCAL_AUT"),
                    "DEV_AUT" => Get("Urls:DEV_AUT"),
                    "TEST_AUT" => Get("Urls:TEST_AUT"),
                    "PROD_AUT" => Get("Urls:PROD_AUT"),
                    _ => Get("Urls:DEV_AUT")
                };

                
                var payload = new WAVE_DTO
                {
                    WAVE = wave
                };

                string urlFinal = $"{url}/BotMulti/create";

                string jsonBody = JsonConvert.SerializeObject(payload);
                var webRequest = (HttpWebRequest)WebRequest.Create(urlFinal);
                webRequest.Accept = "application/json";
                webRequest.Method = "POST";
                webRequest.ContentType = "application/json; charset=utf-8";
                webRequest.Timeout = 150000;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                webRequest.ContentLength = byteArray.Length;

                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                using (var httpResponse = (HttpWebResponse)webRequest.GetResponse())
                using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string error = "";
                
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        error = reader.ReadToEnd();
                    }
 
                }
                throw new Exception($"Error al guardar la wave. {error}", ex);
            }
        }

        public static List<WAVE_DTO> ObtenerWavesNoProcesadas()
        {
            try
            {
                var ambiente = Environment.GetEnvironmentVariable("AMBIENTE_AUTOMATIZACION")
                                ?? ConfigurationManager.AppSettings["Ambiente_Automatizacion"]
                                ?? "DEV_AUT";

                string Get(string key) =>
                    Environment.GetEnvironmentVariable(key.Replace(":", "__"))
                    ?? ConfigurationManager.AppSettings[key];

                var url = ambiente switch
                {
                    "LOCAL_AUT" => Get("Urls:LOCAL_AUT"),
                    "DEV_AUT" => Get("Urls:DEV_AUT"),
                    "TEST_AUT" => Get("Urls:TEST_AUT"),
                    "PROD_AUT" => Get("Urls:PROD_AUT"),
                    _ => Get("Urls:DEV_AUT")
                };

                string urlFinal = $"{url}/BotMulti/ObtenerWavesNoProcesadas";

                var webRequest = (HttpWebRequest)WebRequest.Create(urlFinal);
                webRequest.Accept = "application/json";
                webRequest.Method = "GET";
                webRequest.Timeout = 150000;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var httpResponse = (HttpWebResponse)webRequest.GetResponse())
                using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();

                    return JsonConvert.DeserializeObject<List<WAVE_DTO>>(result)
                           ?? new List<WAVE_DTO>();
                }
            }
            catch (WebException ex)
            {
                string error = "";

                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        error = reader.ReadToEnd();
                    }
                }

                throw new Exception($"Error al obtener las waves no procesadas. {error}", ex);
            }
        }

        public static void ActualizarWave(string wave, string miniwaves)
        {
            try
            {
                var ambiente = Environment.GetEnvironmentVariable("AMBIENTE_AUTOMATIZACION")
                                ?? ConfigurationManager.AppSettings["Ambiente_Automatizacion"]
                                ?? "DEV_AUT";

                string Get(string key) =>
                    Environment.GetEnvironmentVariable(key.Replace(":", "__"))
                    ?? ConfigurationManager.AppSettings[key];

                var url = ambiente switch
                {
                    "LOCAL_AUT" => Get("Urls:LOCAL_AUT"),
                    "DEV_AUT" => Get("Urls:DEV_AUT"),
                    "TEST_AUT" => Get("Urls:TEST_AUT"),
                    "PROD_AUT" => Get("Urls:PROD_AUT"),
                    _ => Get("Urls:DEV_AUT")
                };

                var payload = new UPDATE_WAVE_DTO
                {
                    WAVE = wave,
                    MINIWAVES = miniwaves
                };

                string urlFinal = $"{url}/BotMulti/ActualizarWave";

                string jsonBody = JsonConvert.SerializeObject(payload);

                var webRequest = (HttpWebRequest)WebRequest.Create(urlFinal);
                webRequest.Accept = "application/json";
                webRequest.Method = "POST";
                webRequest.ContentType = "application/json; charset=utf-8";
                webRequest.Timeout = 150000;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                webRequest.ContentLength = byteArray.Length;

                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                using (var httpResponse = (HttpWebResponse)webRequest.GetResponse())
                using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string error = string.Empty;

                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        error = reader.ReadToEnd();
                    }
                }

                throw new Exception($"Error al actualizar la wave. {error}", ex);
            }
        }

        public class WAVE_DTO
        {
            public string WAVE { get; set; }
        }

        public class MINIWAVE
        {
            public string WAVE_KISOFT { get; set; }
        }

        public class UPDATE_WAVE_DTO
        {
            public string WAVE { get; set; }
            public string MINIWAVES { get; set; }
        }
    }
}
