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

        // GET: Actividades/PorProyecto?idProyecto=X
        public ActionResult PorProyecto(int idProyecto)
        {
            try
            {
                // Seguridad básica por sesión
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                // Buscamos únicamente el proyecto seleccionado
                var proyecto = coleccionProyectos.Find(p => p.IdProyecto == idProyecto).FirstOrDefault();

                if (proyecto == null)
                {
                    TempData["Error"] = "El proyecto especificado no existe.";
                    return RedirectToAction("Index", "Proyectos");
                }

                // Pasamos los datos del proyecto padre a la vista mediante ViewBag
                ViewBag.IdProyecto = proyecto.IdProyecto;
                ViewBag.TituloProyecto = proyecto.TituloProyecto;

                // Enviamos directamente la lista de actividades propia del modelo DatosProyecto
                return View(proyecto.Actividades);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar las actividades del proyecto: " + ex.Message;
                return RedirectToAction("Index", "Proyectos");
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

        // GET: Actividades/Agregar
        // Puede recibir o no el idProyecto desde la URL
        public ActionResult Agregar(int? idProyecto)
        {
            try
            {
                // 1. Validación de seguridad por sesión y rol
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para realizar esta acción.";
                    return RedirectToAction("Index");
                }

                var coleccionProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

                // 2. ESCENARIO A: Viene desde un proyecto específico (Contextual)
                if (idProyecto.HasValue)
                {
                    var proyectoFijo = coleccionProyectos.Find(p => p.IdProyecto == idProyecto.Value).FirstOrDefault();
                    if (proyectoFijo != null)
                    {
                        ViewBag.ProyectoFijo = true;
                        ViewBag.IdProyecto = proyectoFijo.IdProyecto;
                        ViewBag.TituloProyecto = proyectoFijo.TituloProyecto;
                    }
                    else
                    {
                        TempData["Error"] = "El proyecto especificado no existe.";
                        return RedirectToAction("Index", "Proyectos");
                    }
                }
                // 3. ESCENARIO B: Entrada global (Cargar Combobox según Rol)
                else
                {
                    ViewBag.ProyectoFijo = false;
                    List<DatosProyecto> listaProyectosFiltrados;

                    if (rolUsuario == "Administrador")
                    {
                        // El Admin puede elegir CUALQUIER proyecto
                        listaProyectosFiltrados = coleccionProyectos.Find(_ => true).ToList();
                    }
                    else // Es Líder
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        // El Líder solo elije proyectos de su semillero
                        listaProyectosFiltrados = coleccionProyectos.Find(p => p.IdSemillero == idSemillero).ToList();
                    }

                    ViewBag.ProyectosMapeados = listaProyectosFiltrados;
                }

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Actividades/Agregar
        [HttpPost]
        public ActionResult Agregar(int idProyecto, string nombreActividad, int duracionValor, string duracionUnidad, string fechaEntregaActividad, bool vinoDesdeProyecto)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                // 1. Validación estricta de fecha en el Servidor (No hoy, no ayer, no pasado)
                DateTime fechaEntrega = DateTime.Parse(fechaEntregaActividad).Date;
                DateTime limiteMinimoEnServidor = DateTime.Today.AddDays(1); // Mañana es el mínimo permitido

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


                int nuevoIdActividad = 400; // Valor por defecto si es la primera actividad del proyecto

                if (proyectoPadre.Actividades != null && proyectoPadre.Actividades.Any())
                {
                    int maxSecuencia = 0;

                    foreach (var act in proyectoPadre.Actividades)
                    {
                        string idStr = act.IdActividad.ToString();

                        // Verificamos que empiece con "40" y tenga una secuencia válida
                        if (idStr.StartsWith("40") && idStr.Length > 2)
                        {
                            // Extraemos todo lo que está después del "40" (la secuencia)
                            if (int.TryParse(idStr.Substring(2), out int secuenciaActual))
                            {
                                if (secuenciaActual > maxSecuencia)
                                {
                                    maxSecuencia = secuenciaActual;
                                }
                            }
                        }
                    }

                    // El siguiente número secuencial
                    int siguienteSecuencia = maxSecuencia + 1;

                    // Concatenamos el prefijo "40" con la nueva secuencia y lo convertimos a entero
                    nuevoIdActividad = int.Parse("40" + siguienteSecuencia);
                }

                // 2. Concatenación inteligente para mantener tu modelo string intacto en MongoDB
                string duracionCompuesta = $"{duracionValor} {duracionUnidad.ToLower()}";

                Actividad nuevaActividad = new Actividad
                {
                    IdActividad = nuevoIdActividad,
                    NombreActividad = nombreActividad.Trim(),
                    DuracionActividad = duracionCompuesta, // Guarda ej: "15 semanas" o "2 meses"
                    FechaEntregaActividad = fechaEntregaActividad,
                    Fases = new List<Fase>()
                };

                var filtro = Builders<DatosProyecto>.Filter.Eq(p => p.IdProyecto, idProyecto);
                var actualizacion = Builders<DatosProyecto>.Update.Push(p => p.Actividades, nuevaActividad);

                coleccionProyectos.UpdateOne(filtro, actualizacion);

                TempData["Exito"] = "Actividad registrada con éxito bajo el ID " + nuevoIdActividad + ".";

                if (vinoDesdeProyecto)
                {
                    return RedirectToAction("PorProyecto", new { idProyecto = idProyecto });
                }

                return RedirectToAction("Index");
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