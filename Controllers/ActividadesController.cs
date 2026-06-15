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

                var listaActividades = proyectos
                    .Where(p => p.Actividades != null && p.Actividades.Any())
                    .SelectMany(p => p.Actividades.Select(a => new DatosActividade
                    {
                        IdProyecto = p.IdProyecto,
                        TituloProyecto = p.TituloProyecto,
                        IdActividad = a.IdActividad,
                        NombreActividad = a.NombreActividad,
                        DuracionActividad = a.DuracionActividad,
                        FechaEntregaActividad = a.FechaEntregaActividad
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
                            // Evalúa si la fecha comienza con el patrón "YYYY-MM" del input
                            listaActividades = listaActividades.Where(a => !string.IsNullOrEmpty(a.FechaEntregaActividad) && a.FechaEntregaActividad.StartsWith(valorFiltro)).ToList();
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

        public ActionResult PorProyecto(int idProyecto)
        {
            try
            {
                // Validación de sesión básica
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                // Buscamos el proyecto específico por su ID
                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyecto == null)
                {
                    TempData["Error"] = "No se encontró el proyecto solicitado.";
                    return RedirectToAction("Index", "Proyectos");
                }

                // Pasamos datos del proyecto a la vista mediante ViewBag para el encabezado
                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;

                // Extraemos las actividades, si no tiene ninguna, creamos una lista vacía
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
                        FechaEntregaActividad = a.FechaEntregaActividad
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

        // GET: Actividades/Agregar?idProyecto=X&flujoSecuencial=true
        [HttpGet]
        public ActionResult Agregar(int? idProyecto, bool flujoSecuencial = false) // <-- El 'int?' permite que sea nulo sin dar error
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

            var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

            // Escenario 1: Viene con un proyecto asignado (Flujo secuencial o desde dentro de un proyecto)
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
            }
            // Escenario 2: Entró por "Gestionar Actividades" (Flujo general libre)
            else
            {
                ViewBag.ProyectoFijo = false;

                // Cargamos todos los proyectos para llenar el combobox (select) de la vista
                var listaProyectos = coleccionProyectos.Find(new MongoDB.Bson.BsonDocument()).ToList();
                ViewBag.ProyectosMapeados = listaProyectos;
            }

            ViewBag.FlujoSecuencial = flujoSecuencial;

            // Mensajes para la alerta flotante de la vista
            if (flujoSecuencial)
            {
                ViewBag.MensajeExitoProyecto = "¡Proyecto creado con éxito!";
                ViewBag.MensajeInstruccion = "Para continuar con el proceso, es obligatorio registrar la primera actividad de este proyecto.";
            }

            return View();
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
        public ActionResult Agregar(int idProyecto, string nombreActividad, int duracionValor, string duracionUnidad, string fechaEntregaActividad, bool vinoDesdeProyecto, bool flujoSecuencial = false)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                // 1. Validación estricta de fecha en el Servidor
                DateTime fechaEntrega = DateTime.Parse(fechaEntregaActividad).Date;
                DateTime limiteMinimoEnServidor = DateTime.Today.AddDays(1);

                if (fechaEntrega < limiteMinimoEnServidor)
                {
                    TempData["Error"] = "Operación rechazada: La fecha de entrega debe ser estrictamente posterior al día de hoy.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var proyectoPadre = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyectoPadre == null)
                {
                    TempData["Error"] = "Error: El proyecto seleccionado ya no existe.";
                    return RedirectToAction("Index");
                }

                // 1. GENERACIÓN DE ID GLOBAL (Prefijo fijo "40")
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
                        if (idStr.StartsWith("40") && idStr.Length >= 3)
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
                    nuevoIdActividad = int.Parse("40" + siguienteSecuencia);
                }

                // 2. Concatenación inteligente
                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                Actividad nuevaActividad = new Actividad
                {
                    IdActividad = nuevoIdActividad,
                    NombreActividad = nombreActividad.Trim(),
                    DuracionActividad = duracionCompuesta,
                    FechaEntregaActividad = fechaEntregaActividad,
                    Fases = new List<Fase>()
                };

                var filtro = Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto);
                var actualizacion = Builders<DatosProyecto>.Update.Push(p => p.Actividades, nuevaActividad);

                coleccionProyectos.UpdateOne(filtro, actualizacion);

                string origenRetorno = vinoDesdeProyecto ? "PorProyecto" : "Index";

                TempData["Exito"] = "Actividad registrada con éxito. Ahora es obligatorio registrar su primera fase.";

                // --- REGLA ABSOLUTA: SIEMPRE IR A FASES ---
                return RedirectToAction("Agregar", "Fases", new
                {
                    idProyecto = idProyecto,
                    idActividad = nuevoIdActividad,
                    flujoSecuencial = flujoSecuencial, // Si viene de proyecto será true, sino false
                    origenActividad = origenRetorno    // <-- Viaja tu lógica de retorno
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error inesperado al guardar: " + ex.Message;
                return RedirectToAction("Index");
            }
        }


        // GET: Actividades/Modificar?idProyecto=X&idActividad=Y&desdeProyecto=true
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

                // Usamos el mismo nombre de ViewBag que usaste en Agregar
                ViewBag.ProyectoFijo = desdeProyecto;
                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;

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
        public ActionResult Modificar(int idProyecto, int idActividad, string nombreActividad, int duracionValor, string duracionUnidad, string fechaEntregaActividad, bool vinoDesdeProyecto) // <- ¡Aquí está la magia!
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                var filtro = Builders<DatosProyecto>.Filter.And(
                    Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto),
                    Builders<DatosProyecto>.Filter.ElemMatch(p => p.Actividades, a => a.IdActividad == idActividad)
                );

                var actualizacion = Builders<DatosProyecto>.Update
                    .Set("actividades.$.nombreActividad", nombreActividad.Trim())
                    .Set("actividades.$.duracionActividad", duracionCompuesta)
                    .Set("actividades.$.fechaEntregaActividad", fechaEntregaActividad);

                coleccionProyectos.UpdateOne(filtro, actualizacion);

                TempData["Exito"] = "Actividad modificada correctamente.";

                // Redirección inteligente igual que en Agregar
                if (vinoDesdeProyecto)
                {
                    return RedirectToAction("PorProyecto", new { idProyecto = idProyecto });
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar las modificaciones: " + ex.Message;
                if (vinoDesdeProyecto)
                {
                    return RedirectToAction("PorProyecto", new { idProyecto = idProyecto });
                }
                return RedirectToAction("Index");
            }
        }
    }
}