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
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

            return View();
        }

        public ActionResult Descargar(string modulo)
        {
            // 1. Sesión
            // La llave real es "Rol" (la misma que usa el header de arriba),
            // no "RolUsuario" como tenía antes -- por eso nunca llegaba el valor.
            string rolUsuario = Session["Rol"]?.ToString() ?? "";
            int idSemilleroUsuario = Convert.ToInt32(Session["IdSemillero"] ?? 0);

            bool esAdmin = rolUsuario.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                        || rolUsuario.Equals("Administrador", StringComparison.OrdinalIgnoreCase);

            // 2. DataSet y DataTable (AHORA DE 6 COLUMNAS)
            DataSet dsGenerico = new DataSet("DataSetGenerico");
            DataTable dtGenerico = new DataTable("TablaGenerica");
            for (int i = 1; i <= 6; i++) { dtGenerico.Columns.Add($"Columna{i}", typeof(string)); }
            dsGenerico.Tables.Add(dtGenerico);

            string tituloReporte = "";
            string[] encabezados = new string[6] { "", "", "", "", "", "" };

            // 3. CONEXIÓN
            var conexion = new Conexion();
            var bd = conexion.Database;
            var colProyectos = bd.GetCollection<DatosProyecto>("Proyectos");

            switch (modulo.ToLower())
            {
                case "usuarios":
                    {
                        tituloReporte = "Reporte de Usuarios - GesSi";
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID" }, { 1, "Nombre" }, { 2, "Rol" }, { 3, "Edad" }, { 4, "Contacto (Tel - Correo)" }, { 5, "Semillero (ID - Nombre)" }
                        });

                        var colUsuarios = bd.GetCollection<DatosUsuario>("Usuarios");
                        var listaUsuarios = esAdmin
                            ? colUsuarios.Find(_ => true).ToList()
                            : colUsuarios.Find(u => u.IdSemillero == idSemilleroUsuario).ToList();

                        // [NUEVO] Cargamos los semilleros en un diccionario para búsqueda rápida
                        var colSemilleros = bd.GetCollection<DatosSemillero>("Semilleros");
                        var dictSemilleros = colSemilleros.Find(_ => true).ToList()
                            .ToDictionary(s => s.IdSemillero, s => s.nombreSemillero);

                        foreach (var user in listaUsuarios)
                        {
                            string tel = user.TelefonoUsuario?.ToString() ?? "N/A";
                            string contacto = $"{tel} - {user.CorreoUsuario}";

                            // [NUEVO] Concatenamos ID y Nombre del Semillero
                            string semilleroInfo = "N/A";
                            if (user.IdSemillero.HasValue)
                            {
                                semilleroInfo = dictSemilleros.ContainsKey(user.IdSemillero.Value)
                                    ? $"{user.IdSemillero.Value} - {dictSemilleros[user.IdSemillero.Value]}"
                                    : user.IdSemillero.Value.ToString();
                            }

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, user.IdUsuario.ToString() },
                                { 1, user.NombreUsuario },
                                { 2, user.RolUsuario },
                                { 3, user.EdadUsuario?.ToString() ?? "N/A" },
                                { 4, contacto },
                                { 5, semilleroInfo } // <-- Aquí pasamos el dato enriquecido
                            }));
                        }
                        break;
                    }

                case "semillero":
                    {
                        tituloReporte = "Reporte de Semilleros - GesSi";
                        // 4 Campos. Dejamos las posiciones 2 y 4 vacías para dar espacio a Nombre y Línea.
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID" }, { 1, "Nombre del Semillero" }, { 3, "Línea de Investigación" }, { 5, "Enfoque" }
                        });

                        var colSemilleros = bd.GetCollection<DatosSemillero>("Semilleros");
                        var listaSemilleros = esAdmin
                            ? colSemilleros.Find(_ => true).ToList()
                            : colSemilleros.Find(s => s.IdSemillero == idSemilleroUsuario).ToList();

                        foreach (var sem in listaSemilleros)
                        {
                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, sem.IdSemillero.ToString() },
                                { 1, sem.nombreSemillero },
                                { 3, sem.LineaSemillero },
                                { 5, sem.EnfoqueSemillero }
                            }));
                        }
                        break;
                    }

                case "proyecto":
                    {
                        tituloReporte = "Reporte de Proyectos - GesSi";
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID" }, { 1, "Título del Proyecto" }, { 3, "Fechas (Inicio - Fin)" }, { 4, "Semillero (ID - Nombre)" }, { 5, "Estado" }
                        });

                        var listaProyectos = esAdmin
                            ? colProyectos.Find(_ => true).ToList()
                            : colProyectos.Find(p => p.IdSemillero == idSemilleroUsuario).ToList();

                        // [NUEVO] Cargamos los semilleros en un diccionario
                        var colSemilleros = bd.GetCollection<DatosSemillero>("Semilleros");
                        var dictSemilleros = colSemilleros.Find(_ => true).ToList()
                            .ToDictionary(s => s.IdSemillero, s => s.nombreSemillero);

                        foreach (var proy in listaProyectos)
                        {
                            string fechas = $"{proy.FechaInicioProyecto:dd/MM/yyyy} a {proy.FechaFinProyecto:dd/MM/yyyy}";

                            // [NUEVO] Concatenamos ID y Nombre del Semillero
                            string semilleroInfo = dictSemilleros.ContainsKey(proy.IdSemillero)
                                ? $"{proy.IdSemillero} - {dictSemilleros[proy.IdSemillero]}"
                                : proy.IdSemillero.ToString();

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, proy.IdProyecto.ToString() },
                                { 1, proy.TituloProyecto },
                                { 3, fechas },
                                { 4, semilleroInfo }, // <-- Pasamos la información enriquecida
                                { 5, proy.Estado }
                            }));
                        }
                        break;
                    }

                case "actividades":
                    {
                        tituloReporte = "Reporte de Actividades - GesSi";
                        // 6 Campos llenos.
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID Act." }, { 1, "Proyecto (ID - Título)" }, { 2, "Nombre Actividad" }, { 3, "Fechas (Inicio - Entrega)" }, { 4, "Duración" }, { 5, "Estado" }
                        });

                        var proyectosFiltrados = esAdmin
                            ? colProyectos.Find(_ => true).ToList()
                            : colProyectos.Find(p => p.IdSemillero == idSemilleroUsuario).ToList();

                        // Almacenamos todo el objeto 'Proyecto' para poder acceder a su ID y Título
                        var listaActividades = proyectosFiltrados
                            .SelectMany(p => (p.Actividades ?? new List<Actividad>())
                                .Select(a => new { Proyecto = p, Act = a }))
                            .ToList();

                        foreach (var item in listaActividades)
                        {
                            // Concatenamos el ID y el Título del proyecto
                            string infoProyecto = $"{item.Proyecto.IdProyecto} - {item.Proyecto.TituloProyecto}";
                            string fechas = $"{item.Act.FechaInicioActividad:dd/MM/yy} a {item.Act.FechaEntregaActividad:dd/MM/yy}";

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, item.Act.IdActividad.ToString() },
                                { 1, infoProyecto },
                                { 2, item.Act.NombreActividad },
                                { 3, fechas },
                                { 4, item.Act.DuracionActividad },
                                { 5, item.Act.EstadoActividad }
                            }));
                        }
                        break;
                    }

                case "fases":
                    {
                        tituloReporte = "Reporte de Fases - GesSi";
                        // 6 Campos llenos. 
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID Fase" }, { 1, "Contexto (Proy / Act)" }, { 2, "Nombre Fase" }, { 3, "Fechas (Inicio - Fin)" }, { 4, "Duración" }, { 5, "Estado" }
                        });

                        var proyectosFiltrados = esAdmin
                            ? colProyectos.Find(_ => true).ToList()
                            : colProyectos.Find(p => p.IdSemillero == idSemilleroUsuario).ToList();

                        // Almacenamos el Proyecto, la Actividad y la Fase
                        var listaFases = proyectosFiltrados
                            .SelectMany(p => (p.Actividades ?? new List<Actividad>())
                                .SelectMany(a => (a.Fases ?? new List<Fase>())
                                    .Select(f => new { Proyecto = p, Act = a, FaseInfo = f })))
                            .ToList();

                        foreach (var item in listaFases)
                        {
                            // Concatenamos el ID y el Nombre tanto del Proyecto como de la Actividad
                            // Usamos " / " para separarlos visualmente. 
                            string infoContexto = $"P:{item.Proyecto.IdProyecto} - {item.Proyecto.TituloProyecto} / A:{item.Act.IdActividad} - {item.Act.NombreActividad}";
                            string fechas = $"{item.FaseInfo.FechaInicio:dd/MM/yy} a {item.FaseInfo.FechaFin:dd/MM/yy}";

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, item.FaseInfo.IdFase.ToString() },
                                { 1, infoContexto },
                                { 2, item.FaseInfo.NombreFase },
                                { 3, fechas },
                                { 4, item.FaseInfo.DuracionFase },
                                { 5, item.FaseInfo.Estado }
                            }));
                        }
                        break;
                    }

                case "reuniones":
                    {
                        tituloReporte = "Reporte de Reuniones - GesSi";
                        encabezados = MapearColumnas(new Dictionary<int, string> {

                            { 0, "ID" }, { 1, "Fecha y Horario" }, { 2, "Lugar" }, { 3, "Motivo" }, { 4, "Líder (ID - Nombre)" }, { 5, "Estado" }
                        });

                        var colReuniones = bd.GetCollection<DatosReunion>("Reuniones");
                        var listaReuniones = esAdmin
                            ? colReuniones.Find(_ => true).ToList()
                            : colReuniones.Find(r => r.IdSemillero == idSemilleroUsuario).ToList();

                        // [NUEVO] Cargamos los usuarios en un diccionario para obtener el nombre del líder
                        var colUsuarios = bd.GetCollection<DatosUsuario>("Usuarios");
                        var dictUsuarios = colUsuarios.Find(_ => true).ToList()
                            .ToDictionary(u => u.IdUsuario, u => u.NombreUsuario);

                        foreach (var reu in listaReuniones)
                        {
                            string fechaHora = $"{reu.FechaReunion:dd/MM/yyyy} ({reu.HoraInicio} - {reu.HoraFin})";

                            // [NUEVO] Buscamos el nombre del líder
                            string liderInfo = dictUsuarios.ContainsKey(reu.IdLider)
                                ? $"{reu.IdLider} - {dictUsuarios[reu.IdLider]}"
                                : reu.IdLider.ToString();

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, reu.IdReunion.ToString() },
                                { 1, fechaHora },
                                { 2, reu.LugarReunion },
                                { 3, reu.MotivoReunion },
                                { 4, liderInfo }, // <-- Pasamos ID y Nombre del líder
                                { 5, reu.EstadoReunion }
                            }));
                        }
                        break;
                    }

                case "eventos":
                    {
                        tituloReporte = "Reporte de Eventos - GesSi";
                        // 6 Campos llenos. Agrupamos Modalidad y Lugar.
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID" }, { 1, "Nombre" }, { 2, "Tipo" }, { 3, "Fecha y Hora" }, { 4, "Modalidad / Lugar" }, { 5, "Estado" }
                        });

                        var colEventos = bd.GetCollection<MongoDB.Bson.BsonDocument>("Eventos");
                        var listaEventos = colEventos.Find(_ => true).ToList();

                        foreach (var doc in listaEventos)
                        {
                            var semilleros = doc["idSemillero"].AsBsonArray.Select(x => x.AsInt32).ToList();
                            if (!esAdmin && !semilleros.Contains(idSemilleroUsuario))
                                continue;

                            string fechaHora = $"{doc["fechaEvento"].AsString} ({doc["horaInicio"].AsString} - {doc["horaFin"].AsString})";
                            string modLugar = $"{doc["modalidad"].AsString} - {doc["lugarEvento"].AsString}";

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, doc["idEvento"].AsInt32.ToString() },
                                { 1, doc["nombreEvento"].AsString },
                                { 2, doc["tipoEvento"].AsString },
                                { 3, fechaHora },
                                { 4, modLugar },
                                { 5, doc["estado"].AsString }
                            }));
                        }
                        break;
                    }

                case "patrocinadores":
                    {
                        tituloReporte = "Reporte de Patrocinadores - GesSi";
                        // 4 Campos. Posiciones 2 y 4 vacías para dar aire al Nombre y Contacto.
                        encabezados = MapearColumnas(new Dictionary<int, string> {
                            { 0, "ID" }, { 1, "Nombre" }, { 3, "Tipo" }, { 5, "Contacto (Tel - Correo)" }
                        });

                        var colPatrocinadores = bd.GetCollection<DatosPatrocinador>("Patrocinadores");
                        var listaPatrocinadores = colPatrocinadores.Find(_ => true).ToList();

                        foreach (var pat in listaPatrocinadores)
                        {
                            string contacto = $"{pat.TelefonoPatrocinador} - {pat.CorreoPatrocinador}";

                            dtGenerico.Rows.Add(MapearColumnas(new Dictionary<int, string> {
                                { 0, pat.IdPatrocinador.ToString() },
                                { 1, pat.NombrePatrocinador },
                                { 3, pat.TipoPatrocinador },
                                { 5, contacto }
                            }));
                        }
                        break;
                    }

                default:
                    return HttpNotFound("Módulo no encontrado.");
            }

            // Si el filtro por rol (ej. el semillero de un líder) no dejó ninguna
            // fila, no tiene sentido exportar: esto evita el ParameterFieldCurrentValueException
            // que se ve cuando Crystal Reports recibe el dataset vacío.
            if (dtGenerico.Rows.Count == 0)
            {
                return Content("No hay datos disponibles para este reporte con tu rol/semillero actual.");
            }

            // 4. Cargar la plantilla
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

            // CAMBIO: Ahora el bucle es de 0 a 6
            for (int i = 0; i < 6; i++)
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
        /// Asigna valores a posiciones exactas (0 al 5) para el dataset de 6 columnas.
        /// Las columnas no asignadas quedarán vacías (""), permitiendo espaciado estratégico.
        /// </summary>
        private string[] MapearColumnas(Dictionary<int, string> posiciones)
        {
            string[] resultado = new string[6] { "", "", "", "", "", "" };

            foreach (var item in posiciones)
            {
                if (item.Key >= 0 && item.Key < 6)
                {
                    resultado[item.Key] = item.Value ?? "";
                }
            }

            return resultado;
        }
    }
}