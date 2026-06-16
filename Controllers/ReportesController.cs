using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class ReportesController : Controller
    {
        // GET: Reportes
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Descargar(string modulo)
        {
            // 1. Sesión
            string rolUsuario = Session["RolUsuario"]?.ToString() ?? "Admin";
            int idSemilleroUsuario = Convert.ToInt32(Session["IdSemillero"] ?? 0);

            // Cualquier rol distinto de Administrador queda restringido a su propio semillero.
            // AJUSTAR si "Investigador" necesita una regla distinta a la de "Lider".
            bool esAdmin = rolUsuario.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                        || rolUsuario.Equals("Administrador", StringComparison.OrdinalIgnoreCase);

            // 2. DataSet y DataTable
            DataSet dsGenerico = new DataSet("DataSetGenerico");
            DataTable dtGenerico = new DataTable("TablaGenerica");
            for (int i = 1; i <= 8; i++) { dtGenerico.Columns.Add($"Columna{i}", typeof(string)); }
            dsGenerico.Tables.Add(dtGenerico);

            string tituloReporte = "";
            string[] encabezados = new string[8] { "", "", "", "", "", "", "", "" };

            // 3. CONEXIÓN
            var conexion = new Conexion();
            var bd = conexion.Database;

            // Se reutiliza en "proyecto", "actividades" y "fases": las actividades y
            // fases no son colecciones propias, vienen anidadas dentro del proyecto.
            var colProyectos = bd.GetCollection<DatosProyecto>("Proyectos");

            switch (modulo.ToLower())
            {
                case "usuarios":
                    {
                        tituloReporte = "Reporte de Usuarios";
                        encabezados = DistribuirColumnas("ID", "Nombre", "Correo", "Rol", "Edad", "Teléfono", "Semillero");

                        var colUsuarios = bd.GetCollection<DatosUsuario>("Usuarios");
                        var listaUsuarios = esAdmin
                            ? colUsuarios.Find(_ => true).ToList()
                            : colUsuarios.Find(u => u.IdSemillero == idSemilleroUsuario).ToList();

                        foreach (var user in listaUsuarios)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                user.IdUsuario.ToString(),
                                user.NombreUsuario,
                                user.CorreoUsuario,
                                user.RolUsuario,
                                user.EdadUsuario?.ToString() ?? "N/A",
                                user.TelefonoUsuario?.ToString() ?? "N/A",
                                user.IdSemillero?.ToString() ?? "N/A"));
                        }
                        break;
                    }

                case "semillero":
                    {
                        tituloReporte = "Reporte de Semilleros - GesSi";
                        encabezados = DistribuirColumnas("ID", "Nombre del Semillero", "Línea de Investigación", "Enfoque");

                        var colSemilleros = bd.GetCollection<DatosSemillero>("Semilleros");
                        var listaSemilleros = esAdmin
                            ? colSemilleros.Find(_ => true).ToList()
                            : colSemilleros.Find(s => s.IdSemillero == idSemilleroUsuario).ToList();

                        foreach (var sem in listaSemilleros)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                sem.IdSemillero.ToString(),
                                sem.nombreSemillero,
                                sem.LineaSemillero,
                                sem.EnfoqueSemillero));
                        }
                        break;
                    }

                case "proyecto":
                    {
                        tituloReporte = "Reporte de Proyectos - GesSi";
                        encabezados = DistribuirColumnas("ID", "Semillero", "Título del Proyecto", "Fecha Inicio", "Fecha Fin", "Estado");

                        var listaProyectos = esAdmin
                            ? colProyectos.Find(_ => true).ToList()
                            : colProyectos.Find(p => p.IdSemillero == idSemilleroUsuario).ToList();

                        foreach (var proy in listaProyectos)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                proy.IdProyecto.ToString(),
                                proy.IdSemillero.ToString(),
                                proy.TituloProyecto,
                                proy.FechaInicioProyecto,
                                proy.FechaFinProyecto,
                                proy.Estado));
                        }
                        break;
                    }

                case "actividades":
                    {
                        tituloReporte = "Reporte de Actividades - GesSi";
                        encabezados = DistribuirColumnas("ID Proy.", "ID Act.", "Nombre Actividad", "Duración", "Fecha Inicio", "Fecha Entrega", "Estado");

                        var proyectosFiltrados = esAdmin
                            ? colProyectos.Find(_ => true).ToList()
                            : colProyectos.Find(p => p.IdSemillero == idSemilleroUsuario).ToList();

                        var listaActividades = proyectosFiltrados
                            .SelectMany(p => (p.Actividades ?? new List<Actividad>())
                                .Select(a => new { IdProyecto = p.IdProyecto, Act = a }))
                            .ToList();

                        foreach (var item in listaActividades)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                item.IdProyecto.ToString(),
                                item.Act.IdActividad.ToString(),
                                item.Act.NombreActividad,
                                item.Act.DuracionActividad,
                                item.Act.FechaInicioActividad,
                                item.Act.FechaEntregaActividad,
                                item.Act.EstadoActividad));
                        }
                        break;
                    }

                case "fases":
                    {
                        tituloReporte = "Reporte de Fases - GesSi";
                        encabezados = DistribuirColumnas("ID Proy.", "ID Fase", "Nombre Fase", "Duración", "Fecha Inicio", "Fecha Fin", "Estado");

                        var proyectosFiltrados = esAdmin
                            ? colProyectos.Find(_ => true).ToList()
                            : colProyectos.Find(p => p.IdSemillero == idSemilleroUsuario).ToList();

                        var listaFases = proyectosFiltrados
                            .SelectMany(p => (p.Actividades ?? new List<Actividad>())
                                .SelectMany(a => (a.Fases ?? new List<Fase>())
                                    .Select(f => new { IdProyecto = p.IdProyecto, FaseInfo = f })))
                            .ToList();

                        foreach (var item in listaFases)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                item.IdProyecto.ToString(),
                                item.FaseInfo.IdFase.ToString(),
                                item.FaseInfo.NombreFase,
                                item.FaseInfo.DuracionFase,
                                item.FaseInfo.FechaInicio.ToString("dd/MM/yyyy"),
                                item.FaseInfo.FechaFin.ToString("dd/MM/yyyy"),
                                item.FaseInfo.Estado));
                        }
                        break;
                    }

                case "reuniones":
                    {
                        tituloReporte = "Reporte de Reuniones - GesSi";
                        encabezados = DistribuirColumnas("ID", "Fecha", "Hora Inicio", "Hora Fin", "Lugar", "Líder", "Estado", "Motivo");

                        // AJUSTAR "Reuniones" si la colección real se llama distinto.
                        var colReuniones = bd.GetCollection<DatosReunion>("Reuniones");
                        var listaReuniones = esAdmin
                            ? colReuniones.Find(_ => true).ToList()
                            : colReuniones.Find(r => r.IdSemillero == idSemilleroUsuario).ToList();

                        foreach (var reu in listaReuniones)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                reu.IdReunion.ToString(),
                                reu.FechaReunion,
                                reu.HoraInicio,
                                reu.HoraFin,
                                reu.LugarReunion,
                                reu.IdLider.ToString(),
                                reu.EstadoReunion,
                                reu.MotivoReunion));
                        }
                        break;
                    }

                case "eventos":
                    {
                        tituloReporte = "Reporte de Eventos - GesSi";
                        encabezados = DistribuirColumnas("ID", "Nombre", "Tipo", "Fecha", "Hora", "Modalidad", "Lugar", "Estado");

                        // Leer directamente como BsonDocument para evitar problemas de deserialización
                        var colEventos = bd.GetCollection<MongoDB.Bson.BsonDocument>("Eventos");
                        var listaEventos = colEventos.Find(_ => true).ToList();

                        foreach (var doc in listaEventos)
                        {
                            // Obtener los semilleros asociados al evento
                            var semilleros = doc["idSemillero"]
                                .AsBsonArray
                                .Select(x => x.AsInt32)
                                .ToList();

                            // Si no es administrador, solo mostrar los eventos de su semillero
                            if (!esAdmin && !semilleros.Contains(idSemilleroUsuario))
                                continue;

                            string horaCompleta = $"{doc["horaInicio"].AsString} - {doc["horaFin"].AsString}";

                            dtGenerico.Rows.Add(DistribuirColumnas(
                                doc["idEvento"].AsInt32.ToString(),
                                doc["nombreEvento"].AsString,
                                doc["tipoEvento"].AsString,
                                doc["fechaEvento"].AsString,
                                horaCompleta,
                                doc["modalidad"].AsString,
                                doc["lugarEvento"].AsString,
                                doc["estado"].AsString
                            ));
                        }

                        break;
                    }

                case "patrocinadores":
                    {
                        tituloReporte = "Reporte de Patrocinadores - GesSi";
                        // El modelo actual no relaciona patrocinadores con un semillero,
                        // así que se muestran a todos los roles. AJUSTAR si deben filtrarse.
                        encabezados = DistribuirColumnas("ID", "Nombre", "Tipo", "Teléfono", "Correo");

                        // AJUSTAR "Patrocinadores" si la colección real se llama distinto.
                        var colPatrocinadores = bd.GetCollection<DatosPatrocinador>("Patrocinadores");
                        var listaPatrocinadores = colPatrocinadores.Find(_ => true).ToList();

                        foreach (var pat in listaPatrocinadores)
                        {
                            dtGenerico.Rows.Add(DistribuirColumnas(
                                pat.IdPatrocinador.ToString(),
                                pat.NombrePatrocinador,
                                pat.TipoPatrocinador,
                                pat.TelefonoPatrocinador.ToString(),
                                pat.CorreoPatrocinador));
                        }
                        break;
                    }

                default:
                    return HttpNotFound("Módulo no encontrado.");
            }

            // 4. Cargar la plantilla (Asegurando la ruta correcta)
            ReportDocument rd = new ReportDocument();
            string rutaReporte = Path.Combine(Server.MapPath("~/Views/Reportes"), "PlantillaGeneral.rpt");

            if (!System.IO.File.Exists(rutaReporte))
            {
                return Content($"Error: No se encontró la plantilla en {rutaReporte}");
            }

            rd.Load(rutaReporte);

            // 5. Asignar los datos
            rd.SetDataSource(dsGenerico);
            rd.SetParameterValue("TituloReporte", tituloReporte);

            for (int i = 0; i < 8; i++)
            {
                rd.SetParameterValue($"Enc{i + 1}", encabezados[i]);
            }

            // 6. Configurar la salida para previsualización (inline)
            Stream stream = rd.ExportToStream(ExportFormatType.PortableDocFormat);
            rd.Close();
            rd.Dispose();

            Response.AppendHeader("Content-Disposition", $"inline; filename=Reporte_GesSi_{modulo}.pdf");

            return File(stream, "application/pdf");
        }

        /// <summary>
        /// Reparte los valores recibidos de forma pareja entre las 8 columnas del
        /// reporte (el primero en la columna 1, el último en la columna 8), en vez
        /// de amontonarlos al inicio. Evita el hueco enorme que queda cuando un
        /// módulo usa menos de 8 campos.
        /// </summary>
        private string[] DistribuirColumnas(params string[] valores)
        {
            const int totalColumnas = 8;
            string[] resultado = new string[totalColumnas];
            for (int i = 0; i < totalColumnas; i++) resultado[i] = "";

            if (valores == null || valores.Length == 0) return resultado;

            if (valores.Length == 1)
            {
                resultado[0] = valores[0];
                return resultado;
            }

            if (valores.Length >= totalColumnas)
            {
                for (int i = 0; i < totalColumnas; i++) resultado[i] = valores[i];
                return resultado;
            }

            for (int i = 0; i < valores.Length; i++)
            {
                int posicion = (int)Math.Round(i * (totalColumnas - 1) / (double)(valores.Length - 1));
                resultado[posicion] = valores[i];
            }

            return resultado;
        }
    }
}