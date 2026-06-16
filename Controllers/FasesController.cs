using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class FasesController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // GET: Fases
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                var builder = Builders<DatosProyecto>.Filter;
                FilterDefinition<DatosProyecto> filtroSeguridad;

                if (rolUsuario == "Administrador")
                {
                    filtroSeguridad = builder.Empty;
                }
                else
                {
                    if (Session["IdSemillero"] != null)
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        filtroSeguridad = builder.Eq(p => p.IdSemillero, idSemillero);
                    }
                    else
                    {
                        TempData["Error"] = "Tu usuario no tiene un semillero asignado correctamente.";
                        return View(new List<DatosFase>());
                    }
                }

                var proyectos = coleccionProyectos.Find(filtroSeguridad).ToList();

                // 1. DENTRO DE LA CONSULTA LINQ: Agregar el mapeo del Título
                var listaFases = proyectos
                    .Where(p => p.Actividades != null && p.Actividades.Any())
                    .SelectMany(p => p.Actividades
                        .Where(a => a.Fases != null && a.Fases.Any())
                        .SelectMany(a => a.Fases.Select(f => new DatosFase
                        {
                            IdProyecto = p.IdProyecto,
                            TituloProyecto = p.TituloProyecto, // Nueva línea
                            IdActividad = a.IdActividad,
                            NombreActividad = a.NombreActividad,
                            IdFase = f.IdFase,
                            NombreFase = f.NombreFase,
                            DuracionFase = f.DuracionFase,
                            FechaInicio = f.FechaInicio,
                            FechaFin = f.FechaFin,
                            Estado = f.Estado ?? "Pendiente"
                        })))
                    .ToList();

                // 2. DENTRO DEL SWITCH DE BÚSQUEDA: Agregar los filtros por Proyecto
                switch (tipoFiltro)
                {
                    case "idFase":
                        listaFases = listaFases.Where(f => f.IdFase.ToString() == valorFiltro).ToList();
                        break;
                    case "nombreFase":
                        listaFases = listaFases.Where(f => f.NombreFase.ToLower().Contains(valorFiltro)).ToList();
                        break;
                    case "idActividad":
                        listaFases = listaFases.Where(f => f.IdActividad.ToString() == valorFiltro).ToList();
                        break;
                    case "nombreActividad":
                        listaFases = listaFases.Where(f => f.NombreActividad.ToLower().Contains(valorFiltro)).ToList();
                        break;
                    // Nuevos filtros
                    case "idProyecto":
                        listaFases = listaFases.Where(f => f.IdProyecto.ToString() == valorFiltro).ToList();
                        break;
                    case "tituloProyecto":
                        listaFases = listaFases.Where(f => f.TituloProyecto.ToLower().Contains(valorFiltro)).ToList();
                        break;
                    case "estado":
                        listaFases = listaFases.Where(f => f.Estado != null && f.Estado.ToLower() == valorFiltro.ToLower()).ToList();
                        break;
                    case "fechaInicio":
                        if (DateTime.TryParse(valorFiltro, out DateTime dateInicio))
                        {
                            listaFases = listaFases.Where(f => f.FechaInicio.Date == dateInicio.Date).ToList();
                        }
                        break;
                    case "fechaFin":
                        if (DateTime.TryParse(valorFiltro, out DateTime dateFin))
                        {
                            listaFases = listaFases.Where(f => f.FechaFin.Date == dateFin.Date).ToList();
                        }
                        break;
                }

                return View(listaFases);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar las fases: " + ex.Message;
                return View(new List<DatosFase>());
            }
        }

        // GET: Fases/PorActividad
        public ActionResult PorActividad(int idProyecto, int idActividad, bool desdeGlobal = false)
        {
            try
            {
                // Seguridad básica por sesión (ajustado a tu ruta)
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                // Buscamos únicamente el proyecto seleccionado
                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyecto == null)
                {
                    TempData["Error"] = "El proyecto especificado no existe.";
                    return RedirectToAction(desdeGlobal ? "Index" : "PorProyecto", "Actividades", new { idProyecto = idProyecto });
                }

                // Buscamos la actividad específica dentro del proyecto
                var actividad = proyecto.Actividades?.FirstOrDefault(a => a.IdActividad == idActividad);

                if (actividad == null)
                {
                    TempData["Error"] = "La actividad especificada no existe.";
                    return RedirectToAction(desdeGlobal ? "Index" : "PorProyecto", "Actividades", new { idProyecto = idProyecto });
                }

                // Pasamos los datos del padre y abuelo a la vista
                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;
                ViewBag.IdActividad = actividad.IdActividad;
                ViewBag.NombreActividad = actividad.NombreActividad;

                // Pasamos la bandera del flujo para configurar el botón de retroceso
                ViewBag.DesdeGlobal = desdeGlobal;

                // Enviamos la lista de fases (si es null, enviamos una lista vacía para evitar errores en el foreach)
                return View(actividad.Fases ?? new List<Fase>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar las fases: " + ex.Message;
                return RedirectToAction("Index", "Actividades");
            }
        }

        [HttpGet]
        public ActionResult Agregar(int? idProyecto, int? idActividad, bool desdeGlobal = false, bool flujoSecuencial = false, string origenActividad = null)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                bool vinoDesdePorActividad = idProyecto.HasValue && idActividad.HasValue;

                ViewBag.VinoDesdePorActividad = vinoDesdePorActividad;
                ViewBag.DesdeGlobal = desdeGlobal;

                // --- GUARDAMOS LAS NUEVAS VARIABLES ---
                ViewBag.FlujoSecuencial = flujoSecuencial;
                ViewBag.OrigenActividad = origenActividad;

                // Configuramos los mensajes de la alerta flotante si aplica
                if (flujoSecuencial)
                {
                    ViewBag.MensajeExito = "¡Actividad registrada con éxito!";
                    ViewBag.MensajeInstruccion = "Para finalizar este registro inicial, es obligatorio crear la primera fase de esta actividad.";
                }

                // ESCENARIO A: Viene de "Fases por Actividad" o del Flujo Secuencial
                if (vinoDesdePorActividad)
                {
                    var proyectoFijo = coleccionProyectos.Find(p => p.IdProyecto == idProyecto.Value).FirstOrDefault();
                    var actividadFija = proyectoFijo?.Actividades?.FirstOrDefault(a => a.IdActividad == idActividad.Value);

                    if (actividadFija != null)
                    {
                        ViewBag.IdProyecto = proyectoFijo.IdProyecto;
                        ViewBag.TituloProyecto = proyectoFijo.TituloProyecto;
                        ViewBag.IdActividad = actividadFija.IdActividad;
                        ViewBag.NombreActividad = actividadFija.NombreActividad;
                    }
                    else
                    {
                        TempData["Error"] = "Datos de origen no encontrados.";
                        return RedirectToAction("Index");
                    }
                }
                // ESCENARIO B: Viene de "Gestionar Fases" (Menú Global)
                else
                {
                    List<DatosProyecto> listaProyectosFiltrados;

                    if (rolUsuario == "Administrador")
                    {
                        listaProyectosFiltrados = coleccionProyectos.Find(_ => true).ToList();
                    }
                    else // Es Líder
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        listaProyectosFiltrados = coleccionProyectos.Find(p => p.IdSemillero == idSemillero).ToList();
                    }

                    ViewBag.ProyectosMapeados = listaProyectosFiltrados;
                }

                // --- DICCIONARIO GLOBAL DE FASES ---
                var todasLasFases = coleccionProyectos.Find(_ => true).ToList()
                    .Where(p => p.Actividades != null)
                    .SelectMany(p => p.Actividades)
                    .Where(a => a.Fases != null)
                    .SelectMany(a => a.Fases)
                    .Select(f => f.NombreFase)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .GroupBy(n => n.Trim().ToLower())
                    .Select(g => g.First())
                    .OrderBy(n => n)
                    .ToList();

                ViewBag.FasesSugeridas = todasLasFases;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Fases/Agregar
        [HttpPost]
        public ActionResult Agregar(FormCollection form)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                // 1. Extraemos los datos del formulario
                int idProyecto = int.Parse(form["idProyecto"]);
                int idActividad = int.Parse(form["idActividad"]);
                string nombreFase = form["nombreFase"];
                int duracionValor = int.Parse(form["duracionValor"]);
                string duracionUnidad = form["duracionUnidad"];
                DateTime fechaInicio = DateTime.Parse(form["fechaInicio"]);
                DateTime fechaFin = DateTime.Parse(form["fechaFin"]);
                string estado = form["estado"];

                bool vinoDesdePorActividad = form["vinoDesdePorActividad"] == "true";
                bool desdeGlobal = form["desdeGlobal"] == "true";

                // --- CAPTURAMOS LAS VARIABLES DE CONTROL DE FLUJO ---
                bool flujoSecuencial = form["flujoSecuencial"] == "true";
                string origenActividad = form["origenActividad"];

                // 2. Buscamos el documento padre completo en MongoDB
                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var proyectoPadre = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyectoPadre == null)
                {
                    TempData["Error"] = "El proyecto no existe.";
                    return RedirectToAction("Index");
                }

                var actividadPadre = proyectoPadre.Actividades?.FirstOrDefault(a => a.IdActividad == idActividad);
                if (actividadPadre == null)
                {
                    TempData["Error"] = "La actividad seleccionada ya no existe.";
                    return RedirectToAction("Index");
                }

                // 3. GENERACIÓN DE ID GLOBAL PARA FASES (Prefijo fijo "50")
                int nuevoIdFase = 501;

                var todasLasFases = coleccionProyectos.Find(_ => true).ToList()
                    .Where(p => p.Actividades != null && p.Actividades.Any())
                    .SelectMany(p => p.Actividades)
                    .Where(a => a.Fases != null && a.Fases.Any())
                    .SelectMany(a => a.Fases)
                    .ToList();

                if (todasLasFases.Any())
                {
                    int maxSecuencia = 0;

                    foreach (var fase in todasLasFases)
                    {
                        string idStr = fase.IdFase.ToString();

                        if (idStr.StartsWith("50") && idStr.Length >= 3)
                        {
                            if (int.TryParse(idStr.Substring(2), out int secuenciaActual))
                            {
                                if (secuenciaActual > maxSecuencia)
                                {
                                    maxSecuencia = secuenciaActual;
                                }
                            }
                        }
                    }

                    int siguienteSecuencia = maxSecuencia + 1;
                    nuevoIdFase = int.Parse("50" + siguienteSecuencia);
                }

                if (actividadPadre.Fases == null)
                {
                    actividadPadre.Fases = new List<Fase>();
                }

                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                // 4. Agregamos la fase en memoria
                actividadPadre.Fases.Add(new Fase
                {
                    IdFase = nuevoIdFase,
                    NombreFase = nombreFase.Trim(),
                    DuracionFase = duracionCompuesta,
                    FechaInicio = fechaInicio,
                    FechaFin = fechaFin,
                    Estado = estado
                });

                // 5. Reemplazamos el documento completo en MongoDB
                coleccionProyectos.ReplaceOne(p => p.IdProyecto == idProyecto, proyectoPadre);

                // --------------------------------------------------------
                // 6. REDIRECCIÓN INTELIGENTE SEGÚN EL ESCENARIO
                // --------------------------------------------------------

                // Escenario A: Flujo completo obligatorio (Creó Proyecto -> Actividad -> Fase)
                if (flujoSecuencial)
                {
                    TempData["Exito"] = "¡Proceso completado! Proyecto, actividad y fase registrados exitosamente.";
                    return RedirectToAction("Index", "Proyectos");
                }

                // A partir de aquí es para los otros flujos
                TempData["Exito"] = "Fase registrada con éxito bajo el ID " + nuevoIdFase + ".";

                // Escenario B: Flujo de 2 pasos (Creó Actividad suelta -> Fase obligatoria)
                if (!string.IsNullOrEmpty(origenActividad))
                {
                    if (origenActividad == "PorProyecto")
                    {
                        // Regresa a la lista de actividades de ese proyecto específico
                        return RedirectToAction("PorProyecto", "Actividades", new { idProyecto = idProyecto });
                    }
                    // Regresa a la gestión global de actividades
                    return RedirectToAction("Index", "Actividades");
                }

                // Escenario C: Flujo simple (Entró a crear solo una fase)
                if (vinoDesdePorActividad)
                {
                    return RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad, desdeGlobal = desdeGlobal });
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar la fase. Detalle: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: Fases/Modificar
        [HttpGet]
        public ActionResult Modificar(int idProyecto, int idActividad, int idFase, bool vinoDesdePorActividad = false, bool desdeGlobal = false)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();
                if (proyecto == null)
                {
                    TempData["Error"] = "El proyecto especificado no existe.";
                    return RedirectToAction("Index");
                }

                var actividad = proyecto.Actividades?.FirstOrDefault(a => a.IdActividad == idActividad);
                if (actividad == null)
                {
                    TempData["Error"] = "La actividad especificada no existe en este proyecto.";
                    return RedirectToAction("Index");
                }

                var fase = actividad.Fases?.FirstOrDefault(f => f.IdFase == idFase);
                if (fase == null)
                {
                    TempData["Error"] = "La fase especificada no existe.";
                    return vinoDesdePorActividad ? RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad }) : RedirectToAction("Index");
                }

                // Pasamos las banderas de flujo
                ViewBag.VinoDesdePorActividad = vinoDesdePorActividad;
                ViewBag.DesdeGlobal = desdeGlobal;

                // Pasamos los datos fijos
                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;
                ViewBag.IdActividad = actividad.IdActividad;
                ViewBag.NombreActividad = actividad.NombreActividad;

                // --- LÓGICA PLUS: DICCIONARIO GLOBAL DE FASES ---
                var todasLasFases = coleccionProyectos.Find(_ => true).ToList()
                    .Where(p => p.Actividades != null)
                    .SelectMany(p => p.Actividades)
                    .Where(a => a.Fases != null)
                    .SelectMany(a => a.Fases)
                    .Select(f => f.NombreFase)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .GroupBy(n => n.Trim().ToLower()) // Agrupa para evitar repetidos
                    .Select(g => g.First()) // Se queda con la versión original (ej: "Análisis")
                    .OrderBy(n => n)
                    .ToList();

                ViewBag.FasesSugeridas = todasLasFases;

                return View(fase);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Fases/Modificar
        [HttpPost]
        public ActionResult Modificar(int idProyecto, int idActividad, int idFase, string nombreFase, int duracionValor, string duracionUnidad, DateTime fechaInicio, DateTime fechaFin, string estado, bool vinoDesdePorActividad, bool desdeGlobal)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                var filtro = Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto);
                var actualizacion = Builders<DatosProyecto>.Update
                    .Set("actividades.$[a].fases.$[f].nombreFase", nombreFase.Trim())
                    .Set("actividades.$[a].fases.$[f].duracionFase", duracionCompuesta)
                    .Set("actividades.$[a].fases.$[f].fechaInicio", fechaInicio)
                    .Set("actividades.$[a].fases.$[f].fechaFin", fechaFin)
                    .Set("actividades.$[a].fases.$[f].estado", estado);

                var arrayFilters = new List<ArrayFilterDefinition>
        {
            new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("a.idActividad", idActividad)),
            new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("f.idFase", idFase))
        };

                var opciones = new UpdateOptions { ArrayFilters = arrayFilters };
                coleccionProyectos.UpdateOne(filtro, actualizacion, opciones);

                TempData["Exito"] = "Fase modificada correctamente.";

                // AQUÍ ESTÁ LA MAGIA DE LA REDIRECCIÓN
                if (vinoDesdePorActividad)
                {
                    // Regresa a la vista de la Actividad específica, respetando si venía del menú global o de proyectos
                    return RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad, desdeGlobal = desdeGlobal });
                }

                // Regresa al index general de Fases
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar las modificaciones: " + ex.Message;

                if (vinoDesdePorActividad)
                {
                    return RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad, desdeGlobal = desdeGlobal });
                }
                return RedirectToAction("Index");
            }
        }

        // GET: Fases/Eliminar
        public ActionResult Eliminar(int idProyecto, int idActividad, int idFase, bool vinoDesdePorActividad = false, bool desdeGlobal = false)
        {
            try
            {
                // 1. Validaciones de seguridad
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para eliminar fases.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                // 2. Buscamos el documento padre
                var proyectoPadre = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();
                if (proyectoPadre == null)
                {
                    TempData["Error"] = "Error: El proyecto especificado no existe.";
                    return vinoDesdePorActividad ? RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad }) : RedirectToAction("Index");
                }

                var actividadPadre = proyectoPadre.Actividades?.FirstOrDefault(a => a.IdActividad == idActividad);
                if (actividadPadre == null)
                {
                    TempData["Error"] = "Error: La actividad especificada no existe.";
                    return vinoDesdePorActividad ? RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad }) : RedirectToAction("Index");
                }

                // 3. Eliminación en memoria
                if (actividadPadre.Fases != null)
                {
                    var faseAEliminar = actividadPadre.Fases.FirstOrDefault(f => f.IdFase == idFase);

                    if (faseAEliminar != null)
                    {
                        actividadPadre.Fases.Remove(faseAEliminar);
                        coleccionProyectos.ReplaceOne(p => p.IdProyecto == idProyecto, proyectoPadre);
                        TempData["Exito"] = "Fase eliminada correctamente.";
                    }
                    else
                    {
                        TempData["Error"] = "La fase ya fue eliminada o no existe.";
                    }
                }

                // 4. Redirección Inteligente (Misma lógica que Modificar)
                if (vinoDesdePorActividad)
                {
                    // Regresa a la tabla de Fases de la Actividad
                    return RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad, desdeGlobal = desdeGlobal });
                }

                // Regresa a la tabla General
                return RedirectToAction("Index");

            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al intentar eliminar la fase: " + ex.Message;

                if (vinoDesdePorActividad)
                {
                    return RedirectToAction("PorActividad", new { idProyecto = idProyecto, idActividad = idActividad, desdeGlobal = desdeGlobal });
                }
                return RedirectToAction("Index");
            }
        }
    }
}