using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class ActividadesController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // GET: Actividades
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                var dictInvestigadores = coleccionUsuarios.Find(u => u.RolUsuario == "Investigador")
                                                          .ToList()
                                                          .ToDictionary(u => u.IdUsuario, u => u.NombreUsuario);

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
                        return View(new List<DatosActividade>());
                    }
                }

                var proyectos = coleccionProyectos.Find(filtroSeguridad).ToList();

                // --- ACTUALIZACIÓN AUTOMÁTICA DE ESTADOS POR TIEMPO ---
                DateTime hoy = DateTime.Today;
                foreach (var p in proyectos)
                {
                    if (p.Actividades != null && p.Actividades.Any())
                    {
                        foreach (var a in p.Actividades)
                        {
                            // Si no está finalizado, evaluamos si hay que cambiar el estado
                            if (a.EstadoActividad != "Finalizado")
                            {
                                if (DateTime.TryParse(a.FechaInicioActividad, out DateTime dInicio) && DateTime.TryParse(a.FechaEntregaActividad, out DateTime dFin))
                                {
                                    string estadoCorrecto = "Pendiente";
                                    if (hoy > dFin.Date) estadoCorrecto = "Retrasado";
                                    else if (hoy >= dInicio.Date && hoy <= dFin.Date) estadoCorrecto = "En Progreso";

                                    if (a.EstadoActividad != estadoCorrecto)
                                    {
                                        a.EstadoActividad = estadoCorrecto;
                                        var filterUpdate = Builders<DatosProyecto>.Filter.And(
                                            Builders<DatosProyecto>.Filter.Eq(proj => proj.IdProyecto, p.IdProyecto),
                                            Builders<DatosProyecto>.Filter.ElemMatch(proj => proj.Actividades, act => act.IdActividad == a.IdActividad)
                                        );
                                        var update = Builders<DatosProyecto>.Update.Set("actividades.$.estadoActividad", estadoCorrecto);
                                        coleccionProyectos.UpdateOne(filterUpdate, update);
                                    }
                                }
                            }
                        }
                    }
                }
                // --- FIN ACTUALIZACIÓN AUTOMÁTICA ---

                var listaActividades = proyectos
                    .Where(p => p.Actividades != null && p.Actividades.Any())
                    .SelectMany(p => p.Actividades.Select(a => new DatosActividade
                    {
                        IdProyecto = p.IdProyecto,
                        TituloProyecto = p.TituloProyecto,
                        IdActividad = a.IdActividad,
                        NombreActividad = a.NombreActividad,
                        DuracionActividad = a.DuracionActividad,
                        FechaInicioActividad = a.FechaInicioActividad,
                        FechaEntregaActividad = a.FechaEntregaActividad,
                        EstadoActividad = a.EstadoActividad ?? "Pendiente",
                        InvestigadoresResponsables = a.InvestigadoresResponsables,
                        NombresInvestigadores = a.InvestigadoresResponsables != null
                            ? a.InvestigadoresResponsables.Select(id => dictInvestigadores.ContainsKey(id) ? dictInvestigadores[id] : "Desconocido").ToList()
                            : new List<string>()
                    }))
                    .ToList();

                // APLICAR FILTROS DE BÚSQUEDA
                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    valorFiltro = valorFiltro.ToLower().Trim();

                    switch (tipoFiltro)
                    {
                        case "idProyecto":
                            listaActividades = listaActividades.Where(a => a.IdProyecto.ToString() == valorFiltro).ToList();
                            break;
                        case "tituloProyecto":
                            listaActividades = listaActividades.Where(a => a.TituloProyecto.ToLower().Contains(valorFiltro)).ToList();
                            break;
                        case "idActividad":
                            listaActividades = listaActividades.Where(a => a.IdActividad.ToString() == valorFiltro).ToList();
                            break;
                        case "nombreActividad":
                            listaActividades = listaActividades.Where(a => a.NombreActividad.ToLower().Contains(valorFiltro)).ToList();
                            break;
                        case "fechaEntregaActividad":
                            listaActividades = listaActividades.Where(a => a.FechaEntregaActividad == valorFiltro).ToList();
                            break;
                        case "mesEntregaActividad":
                            listaActividades = listaActividades.Where(a => !string.IsNullOrEmpty(a.FechaEntregaActividad) && a.FechaEntregaActividad.StartsWith(valorFiltro)).ToList();
                            break;
                        case "estadoActividad":
                            listaActividades = listaActividades.Where(a => a.EstadoActividad.ToLower() == valorFiltro).ToList();
                            break;
                        case "investigadorResponsable":
                            listaActividades = listaActividades.Where(a => a.NombresInvestigadores != null &&
                                                                           a.NombresInvestigadores.Any(n => n.ToLower().Contains(valorFiltro))).ToList();
                            break;
                    }
                }

                return View(listaActividades);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar las actividades: " + ex.Message;
                return View(new List<DatosActividade>());
            }
        }

        // GET: Actividades/PorProyecto
        public ActionResult PorProyecto(int idProyecto)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                var dictInvestigadores = coleccionUsuarios.Find(u => u.RolUsuario == "Investigador")
                                                          .ToList()
                                                          .ToDictionary(u => u.IdUsuario, u => u.NombreUsuario);

                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyecto == null)
                {
                    TempData["Error"] = "No se encontró el proyecto solicitado.";
                    return RedirectToAction("Index", "Proyectos");
                }

                // --- ACTUALIZACIÓN AUTOMÁTICA DE ESTADOS POR TIEMPO ---
                DateTime hoy = DateTime.Today;
                if (proyecto.Actividades != null && proyecto.Actividades.Any())
                {
                    foreach (var a in proyecto.Actividades)
                    {
                        if (a.EstadoActividad != "Finalizado")
                        {
                            if (DateTime.TryParse(a.FechaInicioActividad, out DateTime dInicio) && DateTime.TryParse(a.FechaEntregaActividad, out DateTime dFin))
                            {
                                string estadoCorrecto = "Pendiente";
                                if (hoy > dFin.Date) estadoCorrecto = "Retrasado";
                                else if (hoy >= dInicio.Date && hoy <= dFin.Date) estadoCorrecto = "En Progreso";

                                if (a.EstadoActividad != estadoCorrecto)
                                {
                                    a.EstadoActividad = estadoCorrecto;
                                    var filterUpdate = Builders<DatosProyecto>.Filter.And(
                                        Builders<DatosProyecto>.Filter.Eq(proj => proj.IdProyecto, proyecto.IdProyecto),
                                        Builders<DatosProyecto>.Filter.ElemMatch(proj => proj.Actividades, act => act.IdActividad == a.IdActividad)
                                    );
                                    var update = Builders<DatosProyecto>.Update.Set("actividades.$.estadoActividad", estadoCorrecto);
                                    coleccionProyectos.UpdateOne(filterUpdate, update);
                                }
                            }
                        }
                    }
                }
                // --- FIN ACTUALIZACIÓN AUTOMÁTICA ---

                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;

                var listaActividades = new List<DatosActividade>();

                if (proyecto.Actividades != null && proyecto.Actividades.Any())
                {
                    listaActividades = proyecto.Actividades.Select(a => new DatosActividade
                    {
                        IdProyecto = proyecto.IdProyecto,
                        TituloProyecto = proyecto.TituloProyecto,
                        IdActividad = a.IdActividad,
                        NombreActividad = a.NombreActividad,
                        DuracionActividad = a.DuracionActividad,
                        FechaInicioActividad = a.FechaInicioActividad,
                        FechaEntregaActividad = a.FechaEntregaActividad,
                        EstadoActividad = a.EstadoActividad ?? "Pendiente",
                        InvestigadoresResponsables = a.InvestigadoresResponsables,
                        NombresInvestigadores = a.InvestigadoresResponsables != null
                            ? a.InvestigadoresResponsables.Select(id => dictInvestigadores.ContainsKey(id) ? dictInvestigadores[id] : "Desconocido").ToList()
                            : new List<string>()
                    }).ToList();
                }

                return View(listaActividades);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al cargar las actividades del proyecto: " + ex.Message;
                return RedirectToAction("Index", "Proyectos");
            }
        }

        // GET: Actividades/Agregar
        [HttpGet]
        public ActionResult Agregar(int? idProyecto, bool flujoSecuencial = false)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

            string rolUsuario = Session["Rol"].ToString();
            var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

            ViewBag.Investigadores = new List<DatosUsuario>();

            if (idProyecto.HasValue)
            {
                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto.Value).FirstOrDefault();
                if (proyecto == null)
                {
                    TempData["Error"] = "El proyecto especificado no existe.";
                    return RedirectToAction("Index", "Proyectos");
                }

                ViewBag.IdProyecto = idProyecto.Value;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;
                ViewBag.ProyectoFijo = true;

                ViewBag.FechaInicioProyecto = proyecto.FechaInicioProyecto;
                ViewBag.FechaFinProyecto = proyecto.FechaFinProyecto;

                ViewBag.Investigadores = coleccionUsuarios.Find(u => u.RolUsuario == "Investigador" && u.IdSemillero == proyecto.IdSemillero).ToList();
            }
            else
            {
                ViewBag.ProyectoFijo = false;
                var listaProyectos = coleccionProyectos.Find(new MongoDB.Bson.BsonDocument()).ToList();
                ViewBag.ProyectosMapeados = listaProyectos;

                if ((rolUsuario == "Lider" || rolUsuario == "Líder") && Session["IdSemillero"] != null)
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    ViewBag.Investigadores = coleccionUsuarios.Find(u => u.RolUsuario == "Investigador" && u.IdSemillero == idSemilleroLider).ToList();
                }
            }

            ViewBag.FlujoSecuencial = flujoSecuencial;
            if (flujoSecuencial)
            {
                ViewBag.MensajeExitoProyecto = "¡Proyecto creado con éxito!";
                ViewBag.MensajeInstruccion = "Para continuar con el proceso, es obligatorio registrar la primera actividad de este proyecto.";
            }

            int proximoId = 401;
            var todasLasActividades = coleccionProyectos.Find(_ => true).ToList()
                .Where(p => p.Actividades != null && p.Actividades.Any())
                .SelectMany(p => p.Actividades).ToList();

            if (todasLasActividades.Any())
            {
                int maxSecuencia = 0;
                foreach (var act in todasLasActividades)
                {
                    string idStr = act.IdActividad.ToString();
                    if (idStr.StartsWith("40") && idStr.Length >= 3 && int.TryParse(idStr.Substring(2), out int seq))
                    {
                        if (seq > maxSecuencia) maxSecuencia = seq;
                    }
                }
                proximoId = int.Parse("40" + (maxSecuencia + 1));
            }
            ViewBag.SiguienteId = proximoId;

            return View();
        }

        [HttpGet]
        public JsonResult ObtenerInvestigadoresPorProyecto(int idProyecto)
        {
            try
            {
                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyecto == null)
                {
                    return Json(new { exito = false, mensaje = "Proyecto no encontrado." }, JsonRequestBehavior.AllowGet);
                }

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                var investigadores = coleccionUsuarios.Find(u => u.RolUsuario == "Investigador" && u.IdSemillero == proyecto.IdSemillero).ToList();

                // CORRECCIÓN: Usar NombreUsuario para coincidir con tu modelo y la vista
                var listaLimpiada = investigadores.Select(i => new {
                    IdUsuario = i.IdUsuario,
                    NombreUsuario = i.NombreUsuario
                }).ToList();

                return Json(new { exito = true, data = listaLimpiada }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { exito = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // MODIFICADO: Agregamos el parámetro opcional 'desdeProyecto'
        // GET: Actividades/Eliminar
        public ActionResult Eliminar(int idProyecto, int idActividad, bool desdeProyecto = false)
        {
            try
            {
                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var filtro = Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto);
                var actualizacion = Builders<DatosProyecto>.Update.PullFilter(p => p.Actividades, a => a.IdActividad == idActividad);

                coleccionProyectos.UpdateOne(filtro, actualizacion);
                TempData["Exito"] = "Actividad eliminada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar la actividad: " + ex.Message;
            }

            // Si se eliminó desde la vista específica del proyecto, regresa allí
            if (desdeProyecto)
            {
                return RedirectToAction("PorProyecto", new { idProyecto = idProyecto });
            }

            return RedirectToAction("Index");
        }

        // POST: Actividades/Agregar
        [HttpPost]
        public ActionResult Agregar(int idProyecto, string nombreActividad, string fechaInicioActividad, int duracionValor, string duracionUnidad, string fechaEntregaActividad, bool vinoDesdeProyecto, bool flujoSecuencial = false, string estadoActividad = "Pendiente", int[] investigadoresResponsables = null)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                if (investigadoresResponsables != null && investigadoresResponsables.Length > 2)
                {
                    TempData["Error"] = "Operación rechazada: Solo puedes asignar un máximo de 2 investigadores.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var proyectoPadre = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyectoPadre == null)
                {
                    TempData["Error"] = "Error: El proyecto seleccionado ya no existe.";
                    return RedirectToAction("Index");
                }

                DateTime fechaInicio = DateTime.Parse(fechaInicioActividad).Date;
                DateTime fechaEntrega = DateTime.Parse(fechaEntregaActividad).Date;
                DateTime limiteMinimoEnServidor = DateTime.Today;

                if (fechaInicio < limiteMinimoEnServidor)
                {
                    TempData["Error"] = "Operación rechazada: La fecha de inicio de la actividad no puede ser en el pasado.";
                    return RedirectToAction("Index");
                }

                if (fechaEntrega < fechaInicio)
                {
                    TempData["Error"] = "Operación rechazada: La fecha de entrega no puede ser anterior a la fecha de inicio.";
                    return RedirectToAction("Index");
                }

                if (!string.IsNullOrEmpty(proyectoPadre.FechaInicioProyecto) && !string.IsNullOrEmpty(proyectoPadre.FechaFinProyecto))
                {
                    DateTime inicioProyecto = DateTime.Parse(proyectoPadre.FechaInicioProyecto).Date;
                    DateTime finProyecto = DateTime.Parse(proyectoPadre.FechaFinProyecto).Date;

                    if (fechaInicio < inicioProyecto || fechaInicio > finProyecto)
                    {
                        TempData["Error"] = $"Operación rechazada: El inicio de la actividad debe estar entre las fechas del proyecto ({inicioProyecto.ToShortDateString()} - {finProyecto.ToShortDateString()}).";
                        return RedirectToAction("Index");
                    }

                    if (fechaEntrega > finProyecto)
                    {
                        TempData["Error"] = $"Operación rechazada: La entrega de la actividad no puede superar el límite del proyecto ({finProyecto.ToShortDateString()}).";
                        return RedirectToAction("Index");
                    }
                }

                if (proyectoPadre.Actividades != null && proyectoPadre.Actividades.Any(a => a.NombreActividad.ToLower().Trim() == nombreActividad.ToLower().Trim()))
                {
                    TempData["Error"] = "Operación rechazada: Ya existe una actividad con este mismo nombre en el proyecto.";
                    return RedirectToAction("Index");
                }

                int nuevoIdActividad = 401;
                var todasLasActividades = coleccionProyectos.Find(_ => true).ToList()
                    .Where(p => p.Actividades != null && p.Actividades.Any())
                    .SelectMany(p => p.Actividades)
                    .ToList();

                if (todasLasActividades.Any())
                {
                    int maxSecuencia = 0;
                    foreach (var act in todasLasActividades)
                    {
                        string idStr = act.IdActividad.ToString();
                        if (idStr.StartsWith("40") && idStr.Length >= 3 && int.TryParse(idStr.Substring(2), out int secuenciaActual))
                        {
                            if (secuenciaActual > maxSecuencia) maxSecuencia = secuenciaActual;
                        }
                    }
                    int siguienteSecuencia = maxSecuencia + 1;
                    nuevoIdActividad = int.Parse("40" + siguienteSecuencia);
                }

                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                // --- CÁLCULO DEL ESTADO AL GUARDAR ---
                string estadoCalculado = "Pendiente";
                DateTime hoy = DateTime.Today;
                if (estadoActividad != "Finalizado")
                {
                    if (hoy > fechaEntrega.Date) estadoCalculado = "Retrasado";
                    else if (hoy >= fechaInicio.Date && hoy <= fechaEntrega.Date) estadoCalculado = "En Progreso";
                }
                else
                {
                    estadoCalculado = "Finalizado";
                }

                Actividad nuevaActividad = new Actividad
                {
                    IdActividad = nuevoIdActividad,
                    NombreActividad = nombreActividad.Trim(),
                    FechaInicioActividad = fechaInicioActividad,
                    DuracionActividad = duracionCompuesta,
                    FechaEntregaActividad = fechaEntregaActividad,
                    EstadoActividad = estadoCalculado, // Usamos el calculado
                    InvestigadoresResponsables = investigadoresResponsables != null ? investigadoresResponsables.ToList() : new List<int>(),
                    Fases = new List<Fase>()
                };

                var filtro = Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto);
                var actualizacion = Builders<DatosProyecto>.Update.Push(p => p.Actividades, nuevaActividad);

                coleccionProyectos.UpdateOne(filtro, actualizacion);

                string origenRetorno = vinoDesdeProyecto ? "PorProyecto" : "Index";

                TempData["Exito"] = "Actividad registrada con éxito. Ahora es obligatorio registrar su primera fase.";

                return RedirectToAction("Agregar", "Fases", new
                {
                    idProyecto = idProyecto,
                    idActividad = nuevoIdActividad,
                    flujoSecuencial = flujoSecuencial,
                    origenActividad = origenRetorno
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error inesperado al guardar: " + ex.Message;
                return RedirectToAction("Index");
            }
        }


        [HttpGet]
        public ActionResult Modificar(int idProyecto, int idActividad, bool desdeProyecto = false)
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
                    return desdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                }

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                ViewBag.InvestigadoresSemillero = coleccionUsuarios.Find(u => u.RolUsuario == "Investigador" && u.IdSemillero == proyecto.IdSemillero).ToList();

                ViewBag.ProyectoFijo = desdeProyecto;
                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;

                ViewBag.FechaInicioProyecto = proyecto.FechaInicioProyecto;
                ViewBag.FechaFinProyecto = proyecto.FechaFinProyecto;

                return View(actividad);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al abrir el formulario de modificación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Actividades/Modificar
        [HttpPost]
        public ActionResult Modificar(int idProyecto, int idActividad, string nombreActividad, string fechaInicioActividad, int duracionValor, string duracionUnidad, string fechaEntregaActividad, bool vinoDesdeProyecto, string estadoActividad, int[] investigadoresResponsables = null)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                if (investigadoresResponsables != null && investigadoresResponsables.Length > 2)
                {
                    TempData["Error"] = "Operación rechazada: Solo puedes asignar un máximo de 2 investigadores.";
                    return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var proyectoPadre = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyectoPadre == null)
                {
                    TempData["Error"] = "Error: El proyecto seleccionado ya no existe.";
                    return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                }

                DateTime fechaInicio = DateTime.Parse(fechaInicioActividad).Date;
                DateTime fechaEntrega = DateTime.Parse(fechaEntregaActividad).Date;

                if (fechaEntrega < fechaInicio)
                {
                    TempData["Error"] = "Operación rechazada: La fecha de entrega no puede ser anterior a la fecha de inicio.";
                    return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                }

                if (!string.IsNullOrEmpty(proyectoPadre.FechaInicioProyecto) && !string.IsNullOrEmpty(proyectoPadre.FechaFinProyecto))
                {
                    DateTime inicioProyecto = DateTime.Parse(proyectoPadre.FechaInicioProyecto).Date;
                    DateTime finProyecto = DateTime.Parse(proyectoPadre.FechaFinProyecto).Date;

                    if (fechaInicio < inicioProyecto || fechaInicio > finProyecto)
                    {
                        TempData["Error"] = $"Operación rechazada: El inicio de la actividad debe estar entre las fechas del proyecto ({inicioProyecto.ToShortDateString()} - {finProyecto.ToShortDateString()}).";
                        return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                    }

                    if (fechaEntrega > finProyecto)
                    {
                        TempData["Error"] = $"Operación rechazada: La entrega de la actividad no puede superar el límite del proyecto ({finProyecto.ToShortDateString()}).";
                        return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                    }
                }

                if (proyectoPadre.Actividades != null && proyectoPadre.Actividades.Any(a => a.IdActividad != idActividad && a.NombreActividad.ToLower().Trim() == nombreActividad.ToLower().Trim()))
                {
                    TempData["Error"] = "Operación rechazada: Ya existe otra actividad con este mismo nombre en el proyecto.";
                    return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
                }

                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                // --- CÁLCULO DEL ESTADO AL MODIFICAR ---
                string estadoCalculado = "Pendiente";
                DateTime hoy = DateTime.Today;
                if (estadoActividad != "Finalizado")
                {
                    if (hoy > fechaEntrega.Date) estadoCalculado = "Retrasado";
                    else if (hoy >= fechaInicio.Date && hoy <= fechaEntrega.Date) estadoCalculado = "En Progreso";
                }
                else
                {
                    estadoCalculado = "Finalizado";
                }

                var filtro = Builders<DatosProyecto>.Filter.And(
                    Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto),
                    Builders<DatosProyecto>.Filter.ElemMatch(p => p.Actividades, a => a.IdActividad == idActividad)
                );

                var actualizacion = Builders<DatosProyecto>.Update
                    .Set("actividades.$.nombreActividad", nombreActividad.Trim())
                    .Set("actividades.$.fechaInicioActividad", fechaInicioActividad)
                    .Set("actividades.$.duracionActividad", duracionCompuesta)
                    .Set("actividades.$.fechaEntregaActividad", fechaEntregaActividad)
                    .Set("actividades.$.estadoActividad", estadoCalculado) // Usamos el calculado
                    .Set("actividades.$.investigadoresResponsables", investigadoresResponsables != null ? investigadoresResponsables.ToList() : new List<int>());

                coleccionProyectos.UpdateOne(filtro, actualizacion);

                TempData["Exito"] = "Actividad modificada correctamente.";

                if (vinoDesdeProyecto)
                {
                    return RedirectToAction("PorProyecto", new { idProyecto = idProyecto });
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar las modificaciones: " + ex.Message;
                return vinoDesdeProyecto ? RedirectToAction("PorProyecto", new { idProyecto = idProyecto }) : RedirectToAction("Index");
            }
        }
    }
}