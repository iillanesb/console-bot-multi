using System;
using System.IO;

namespace WmosAutomatizacion.Helpers
{
    public class LoggerService
    {
        private readonly string _basePath;
        private readonly string _procesandoPath;
        private readonly string _okPath;
        private readonly string _errorPath;

        private string _currentLogFile;

        public LoggerService()
        {
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGS");

            _procesandoPath = Path.Combine(_basePath, "PROCESANDO");
            _okPath = Path.Combine(_basePath, "OK");
            _errorPath = Path.Combine(_basePath, "ERROR");

            Directory.CreateDirectory(_procesandoPath);
            Directory.CreateDirectory(_okPath);
            Directory.CreateDirectory(_errorPath);

            string fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            _currentLogFile = Path.Combine(
                _procesandoPath,
                $"LOG_{fecha}.txt");

            File.WriteAllText(_currentLogFile, "");
        }

        public void Log(string mensaje)
        {
            File.AppendAllText(
                _currentLogFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {mensaje}{Environment.NewLine}");
        }

        public void ActualizarNombreConWave(string waveNumber)
        {
            if (string.IsNullOrWhiteSpace(waveNumber))
                return;

            string fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string nuevoArchivo = Path.Combine(
                _procesandoPath,
                $"{waveNumber}_{fecha}.txt");

            if (!File.Exists(nuevoArchivo))
            {
                File.Move(_currentLogFile, nuevoArchivo);
                _currentLogFile = nuevoArchivo;
            }
        }

        public void FinalizarOk()
        {
            Log("EXITOSO");

            string destino = Path.Combine(
                _okPath,
                Path.GetFileName(_currentLogFile));

            if (File.Exists(destino))
                File.Delete(destino);

            File.Move(_currentLogFile, destino);

            _currentLogFile = destino;
        }

        public void FinalizarError(Exception ex)
        {
            Log("ERROR");
            Log(ex.ToString());

            string destino = Path.Combine(
                _errorPath,
                Path.GetFileName(_currentLogFile));

            if (File.Exists(destino))
                File.Delete(destino);

            File.Move(_currentLogFile, destino);

            _currentLogFile = destino;
        }
    }
}