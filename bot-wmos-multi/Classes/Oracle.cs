using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OracleClient;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace Api.Clases
{
    public class Oracle
    {

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const string BaseUrl = "https://lof.falabella.cl/giru-service/oracle/Get-Credenciales/";


        public static async Task<List<T>> ConsultaOracleDatasetAsync<T>(string consulta, string tipo) where T : new()
        {
            var idCredencial = 0;
            var Tns = "";
            switch (tipo)
            {
                case "WMS_LOF2_WF":
                    idCredencial = 64;
                    Tns = "(DESCRIPTION=(CONNECT_DATA=(SERVICE_NAME= wmfaclpr))(ADDRESS=(PROTOCOL=TCP)(HOST=meridio-sb.falabella.cl)(PORT=1531)))";
                    break;
            }
            string password = "noencotrado";

            try
            {
                var cred = await ObtenerCredenciales(idCredencial);
                password = @$"User Id={cred.USUARIO};Password={cred.PASSWORD};Data Source={Tns}";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fallo al obtener credenciales: {ex.Message}");
            }




            OracleConnection cn = null;
            if (password == "noencotrado") return null;

            try
            {
                DataSet ds = new DataSet();
                DataTable dt = new DataTable();
                List<T> devolver = new List<T>();
                cn = new OracleConnection(password);

                cn.Open();
                OracleDataAdapter adpter = new OracleDataAdapter(consulta, cn);
                adpter.Fill(ds);
                cn.Close();

                if (ds != null)
                {
                    dt = ds.Tables[0];
                    foreach (DataRow row in dt.Rows)
                    {
                        T item = CreateItemFromRow<T>(row);
                        if (item != null)
                        {
                            devolver.Add(item);
                        }
                    }
                }
                return (devolver);
            }
            catch (Exception e)
            {
                //SaveException.Internal(e);
                return null;
            }
            finally
            {
                if (cn != null && cn.State == System.Data.ConnectionState.Open)
                {
                    cn.Close();
                }
            }
        }
        public static T CreateItemFromRow<T>(DataRow row) where T : new()
        {
            T item = new T();
            SetItemFromRow(item, row);
            return item;
        }

        public static void SetItemFromRow<T>(T item, DataRow row) where T : new()
        {
            foreach (DataColumn c in row.Table.Columns)
            {
                PropertyInfo p = item.GetType().GetProperty(c.ColumnName);
                if (p != null && row[c] != DBNull.Value)
                {
                    object value = ConvertValue(row[c], p.PropertyType);
                    p.SetValue(item, value);
                }
            }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            // Intenta convertir el valor al tipo objetivo
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch (Exception e)
            {
                //SaveException.Internal(e);
                return null;
            }
        }


        public class Credenciales
        {
            public string USUARIO { get; set; } = string.Empty;
            public string PASSWORD { get; set; } = string.Empty;
        }


        //public static async Task<Credenciales?> ObtenerCredenciales(int idCredencial, CancellationToken ct = default)
        //{
        //    var url = $"{BaseUrl}{idCredencial}";

        //    using var req = new HttpRequestMessage(HttpMethod.Get, url);
        //    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        //    if (!resp.IsSuccessStatusCode)
        //    {
        //        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        //        throw new HttpRequestException(
        //            $"Error { (int)resp.StatusCode } al obtener credenciales. Respuesta: {body}");
        //    }

        //    await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        //    var options = new JsonSerializerOptions
        //    {
        //        PropertyNameCaseInsensitive = false
        //    };

        //    var cred = await JsonSerializer.DeserializeAsync<Credenciales>(stream, options, ct)
        //               .ConfigureAwait(false);

        //    if (cred is null || string.IsNullOrWhiteSpace(cred.USUARIO) || string.IsNullOrWhiteSpace(cred.PASSWORD))
        //        throw new InvalidOperationException("La respuesta no contenía credenciales válidas.");

        //    return cred;
        //}

        public static async Task<Credenciales> ObtenerCredenciales( int idCredencial, CancellationToken ct = default(CancellationToken))
        {
            var url = BaseUrl + idCredencial;

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                                         .ConfigureAwait(false))
            {
                if (!resp.IsSuccessStatusCode)
                {
                    var bodyError = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    throw new HttpRequestException(
                        string.Format("Error {0} al obtener credenciales. Respuesta: {1}",
                        (int)resp.StatusCode,
                        bodyError));
                }

                using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = false
                    };

                    var cred = await JsonSerializer.DeserializeAsync<Credenciales>(
                        stream,
                        options,
                        ct).ConfigureAwait(false);

                    if (cred == null ||
                        string.IsNullOrWhiteSpace(cred.USUARIO) ||
                        string.IsNullOrWhiteSpace(cred.PASSWORD))
                    {
                        throw new InvalidOperationException(
                            "La respuesta no contenía credenciales válidas.");
                    }

                    return cred;
                }
            }
        }


    }
}