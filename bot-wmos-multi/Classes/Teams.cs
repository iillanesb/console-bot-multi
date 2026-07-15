using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using static WmosAutomatizacion.Classes.Program;
using static WmosAutomatizacionNew.Classes.WMOS;

namespace WmosAutomatizacionNew.Classes
{
    class Teams
    {
        public static void enviarMensajeTeams(WaveDetails wave)
        {
            try
            {
                var ambiente = Environment.GetEnvironmentVariable("AMBIENTE")
                                        ?? ConfigurationManager.AppSettings["Ambiente"]
                                        ?? "DEV";

                string Get(string key) => Environment.GetEnvironmentVariable(key.Replace(":", "__"))
                                                        ?? ConfigurationManager.AppSettings[key];

                var url = ambiente switch
                {
                    "LOCAL" => Get("Urls:LOCAL"),
                    "DEV" => Get("Urls:DEV"),
                    "TEST" => Get("Urls:TEST"),
                    "PROD" => Get("Urls:PROD"),
                    _ => Get("Urls:DEV")
                };


                var payload = wave;
                string jsonBody = JsonConvert.SerializeObject(payload);

                // Crea la solicitud POST
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.Accept = "application/json";
                webRequest.Method = "POST";
                webRequest.ContentType = "application/json; charset=utf-8";
                webRequest.Timeout = 150000;

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // Escribir el body
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                webRequest.ContentLength = byteArray.Length;
                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                // Obtener la respuesta
                using (var httpResponse = (HttpWebResponse)webRequest.GetResponse())
                using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {

                throw;
            }
        }

    }
}
