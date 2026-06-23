using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Configuration;

namespace ProyectoSemillero_ASP.NET.Services
{
    public class GeminiService
    {
        private readonly string _apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public async Task<string> GenerarRespuestaAsistente(string promptUsuario)
        {
            try
            {
                // Obtenemos la llave de manera segura desde el web.config antes de la petición
                string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var client = new HttpClient())
                {
                    string urlConKey = $"{_apiUrl}?key={apiKey}";

                    var cuerpoRequest = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = promptUsuario } } }
                        }
                    };

                    var serializer = new JavaScriptSerializer();
                    string jsonPayload = serializer.Serialize(cuerpoRequest);

                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(urlConKey, content);

                    string jsonRespuesta = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic datosResultantes = serializer.Deserialize<dynamic>(jsonRespuesta);
                        var candidatos = datosResultantes["candidates"] as object[];
                        var primerCandidato = candidatos[0] as dynamic;
                        var contenido = primerCandidato["content"] as dynamic;
                        var partes = contenido["parts"] as object[];
                        var primeraParte = partes[0] as dynamic;

                        return primeraParte["text"].ToString();
                    }
                    else
                    {
                        return $"**Error de la API:** {jsonRespuesta}";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"**Excepción:** {ex.Message}";
            }
        }
    }
}