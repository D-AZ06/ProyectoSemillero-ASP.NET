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
    public class EventosController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // =============================================
        // HELPER PRIVADO: carga proyectos según el rol
        // =============================================
        private List<DatosProyecto> ObtenerProyectosSegunRol()
        {
            string rol = Session["Rol"]?.ToString();
            var colProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");

            if (rol == "Administrador" || rol == "Admin")
                return colProyectos.Find(_ => true).ToList();

            if (Session["IdSemillero"] != null)
            {
                int idSemillero = (int)Session["IdSemillero"];
                return colProyectos.Find(p => p.IdSemillero == idSemillero).ToList();
            }

            return new List<DatosProyecto>();
        }

        // =============================================
        // GET: Eventos
        // =============================================
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");

                var builder = Builders<DatosEvento>.Filter;
                FilterDefinition<DatosEvento> filtroSeguridad;

                if (rolUsuario == "Administrador" || rolUsuario == "Admin")
                {
                    filtroSeguridad = builder.Empty;
                }
                else
                {
                    if (Session["IdSemillero"] != null)
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        filtroSeguridad = builder.Eq(e => e.IdSemillero, idSemillero);
                    }
                    else
                    {
                        TempData["Error"] = "Tu usuario no tiene un semillero asignado correctamente.";
                        return View(new List<DatosEvento>());
                    }
                }

                FilterDefinition<DatosEvento> filtroBusqueda = builder.Empty;

                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    switch (tipoFiltro)
                    {
                        case "idEvento":
                            if (int.TryParse(valorFiltro, out int idEv))
                                filtroBusqueda = builder.Eq(e => e.IdEvento, idEv);
                            break;
                        case "nombreEvento":
                            filtroBusqueda = builder.Regex(e => e.NombreEvento, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "tipoEvento":
                            filtroBusqueda = builder.Regex(e => e.TipoEvento, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "lugarEvento":
                            filtroBusqueda = builder.Regex(e => e.LugarEvento, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "fechaEvento":
                            filtroBusqueda = builder.Eq(e => e.FechaEvento, valorFiltro);
                            break;
                        case "mesEvento":
                            filtroBusqueda = builder.Regex(e => e.FechaEvento, new BsonRegularExpression($"^{valorFiltro}"));
                            break;
                    }
                }

                var filtroFinal = builder.And(filtroSeguridad, filtroBusqueda);
                var lista = coleccionEventos.Find(filtroFinal).SortByDescending(e => e.FechaEvento).ToList();

                return View(lista);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los eventos: " + ex.Message;
                return View(new List<DatosEvento>());
            }
        }

        // =============================================
        // GET: Eventos/Detalle/id
        // =============================================
        public ActionResult Detalle(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var evento = coleccionEventos.Find(e => e.IdEvento == id).FirstOrDefault();

                if (evento == null)
                {
                    TempData["Error"] = "El evento solicitado no existe.";
                    return RedirectToAction("Index");
                }

                // NUEVO: Cargamos el catálogo global de patrocinadores para pasarlo al modal modalPatrocinador
                var coleccionPatrocinadores = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");
                ViewBag.CatalogoPatrocinadores = coleccionPatrocinadores.Find(_ => true).ToList();

                return View(evento);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el detalle: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // GET: Eventos/Agregar
        // =============================================
        [HttpGet]
        public ActionResult Agregar()
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para agregar eventos.";
                    return RedirectToAction("Index");
                }

                // Calcular próximo ID (prefijo 60)
                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var ultimoEvento = coleccionEventos.Find(new BsonDocument())
                                                   .SortByDescending(e => e.IdEvento)
                                                   .FirstOrDefault();
                int correlativo = 1;
                if (ultimoEvento != null)
                {
                    string ultimoIdStr = ultimoEvento.IdEvento.ToString();
                    if (ultimoIdStr.StartsWith("60"))
                    {
                        if (int.TryParse(ultimoIdStr.Substring(2), out int numero))
                            correlativo = numero + 1;
                    }
                }
                ViewBag.SiguienteId = int.Parse("60" + correlativo);

                // Proyectos disponibles según rol
                ViewBag.ProyectosDisponibles = ObtenerProyectosSegunRol();

                // Para el Líder: mostrar su semillero fijo
                if (rolUsuario == "Líder")
                {
                    int idSemillero = (int)Session["IdSemillero"];
                    ViewBag.IdSemilleroFijo = idSemillero;

                    var colSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                    var semillero = colSemilleros.Find(s => s.IdSemillero == idSemillero).FirstOrDefault();
                    ViewBag.NombreSemilleroFijo = semillero?.nombreSemillero ?? "Tu semillero";
                }
                else
                {
                    var colSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                    var semilleros = colSemilleros.Find(_ => true).ToList()
                        .Select(s => new { IdSemillero = s.IdSemillero, NombreSemillero = s.nombreSemillero })
                        .ToList();
                    ViewBag.ListaSemilleros = new SelectList(semilleros, "IdSemillero", "NombreSemillero");
                }

                ViewBag.RolUsuario = rolUsuario;
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // POST: Eventos/Agregar
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosEvento nuevoEvento, int[] proyectosSeleccionados)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para agregar eventos.";
                    return RedirectToAction("Index");
                }

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");

                // Generar ID con prefijo 60
                var ultimoEvento = coleccionEventos.Find(new BsonDocument())
                                                   .SortByDescending(e => e.IdEvento)
                                                   .FirstOrDefault();
                int correlativo = 1;
                if (ultimoEvento != null)
                {
                    string ultimoIdStr = ultimoEvento.IdEvento.ToString();
                    if (ultimoIdStr.StartsWith("60"))
                    {
                        if (int.TryParse(ultimoIdStr.Substring(2), out int numero))
                            correlativo = numero + 1;
                    }
                }
                nuevoEvento.IdEvento = int.Parse("60" + correlativo);

                // Forzar semillero al Líder
                if (rolUsuario == "Líder")
                    nuevoEvento.IdSemillero = (int)Session["IdSemillero"];

                // Vincular proyectos seleccionados
                nuevoEvento.ProyectosParticipantes = new List<ProyectoParticipante>();
                if (proyectosSeleccionados != null && proyectosSeleccionados.Length > 0)
                {
                    var colProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                    foreach (int idProy in proyectosSeleccionados)
                    {
                        var proy = colProyectos.Find(p => p.IdProyecto == idProy).FirstOrDefault();
                        if (proy != null)
                        {
                            nuevoEvento.ProyectosParticipantes.Add(new ProyectoParticipante
                            {
                                IdProyecto = proy.IdProyecto,
                                TituloProyecto = proy.TituloProyecto
                            });
                        }
                    }
                }

                // NUEVO: Instancia la nueva lista apuntando a DatosPatrocinador vacía
                nuevoEvento.Patrocinadores = new List<DatosPatrocinador>();

                coleccionEventos.InsertOne(nuevoEvento);
                TempData["Exito"] = $"Evento '{nuevoEvento.NombreEvento}' registrado con ID: {nuevoEvento.IdEvento}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar el evento: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // GET: Eventos/Modificar/id
        // =============================================
        [HttpGet]
        public ActionResult Modificar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar eventos.";
                    return RedirectToAction("Index");
                }

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var evento = coleccionEventos.Find(e => e.IdEvento == id).FirstOrDefault();

                if (evento == null)
                {
                    TempData["Error"] = "El evento solicitado no existe.";
                    return RedirectToAction("Index");
                }

                // Validar que el Líder solo modifique sus eventos
                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    if (evento.IdSemillero != idSemilleroLider)
                    {
                        TempData["Error"] = "Acceso denegado: Este evento pertenece a otro semillero.";
                        return RedirectToAction("Index");
                    }
                }

                ViewBag.ProyectosDisponibles = ObtenerProyectosSegunRol();
                ViewBag.RolUsuario = rolUsuario;

                return View(evento);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // POST: Eventos/Modificar
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosEvento eventoModificado, int[] proyectosSeleccionados)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar eventos.";
                    return RedirectToAction("Index");
                }

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var filtro = Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, eventoModificado.IdEvento);

                // Candado para Líder
                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    filtro = Builders<DatosEvento>.Filter.And(
                        Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, eventoModificado.IdEvento),
                        Builders<DatosEvento>.Filter.Eq(e => e.IdSemillero, idSemilleroLider)
                    );
                }

                var eventoOriginal = coleccionEventos.Find(filtro).FirstOrDefault();

                if (eventoOriginal == null)
                {
                    TempData["Error"] = "No se encontró el evento o no tienes permisos.";
                    return RedirectToAction("Index");
                }

                // Preservar datos vitales
                eventoModificado.Id = eventoOriginal.Id;
                eventoModificado.IdSemillero = eventoOriginal.IdSemillero;
                eventoModificado.Patrocinadores = eventoOriginal.Patrocinadores ?? new List<DatosPatrocinador>();

                // Actualizar proyectos participantes
                eventoModificado.ProyectosParticipantes = new List<ProyectoParticipante>();
                if (proyectosSeleccionados != null && proyectosSeleccionados.Length > 0)
                {
                    var colProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                    foreach (int idProy in proyectosSeleccionados)
                    {
                        var proy = colProyectos.Find(p => p.IdProyecto == idProy).FirstOrDefault();
                        if (proy != null)
                        {
                            eventoModificado.ProyectosParticipantes.Add(new ProyectoParticipante
                            {
                                IdProyecto = proy.IdProyecto,
                                TituloProyecto = proy.TituloProyecto
                            });
                        }
                    }
                }

                coleccionEventos.ReplaceOne(filtro, eventoModificado);
                TempData["Exito"] = "El evento ha sido actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar las modificaciones: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // GET: Eventos/Eliminar/id
        // =============================================
        public ActionResult Eliminar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para eliminar eventos.";
                    return RedirectToAction("Index");
                }

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                DeleteResult resultado;

                if (rolUsuario == "Administrador" || rolUsuario == "Admin")
                {
                    resultado = coleccionEventos.DeleteOne(e => e.IdEvento == id);
                }
                else
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    resultado = coleccionEventos.DeleteOne(e => e.IdEvento == id && e.IdSemillero == idSemilleroLider);
                }

                if (resultado.DeletedCount > 0)
                    TempData["Exito"] = "El evento ha sido eliminado correctamente.";
                else
                    TempData["Error"] = "No se pudo eliminar el evento o no tienes permisos.";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // NUEVO POST AJAX: VincularPatrocinadorExistente
        // =============================================
        [HttpPost]
        public JsonResult VincularPatrocinadorExistente(int idEvento, int idPatrocinador)
        {
            try
            {
                if (Session["Rol"] == null) return Json(new { success = false, message = "Sesión expirada." });

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador") return Json(new { success = false, message = "Sin permisos para vincular aliados." });

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var coleccionPatrocinadores = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");

                // 1. Ir al catálogo global y traer los datos limpios de la organización
                var patrocinadorGlobal = coleccionPatrocinadores.Find(p => p.IdPatrocinador == idPatrocinador).FirstOrDefault();
                if (patrocinadorGlobal == null)
                {
                    return Json(new { success = false, message = "El patrocinador seleccionado no existe en el catálogo global." });
                }

                // 2. Comprobar la existencia del evento y que no esté ya asociado para evitar duplicación
                var eventoActual = coleccionEventos.Find(e => e.IdEvento == idEvento).FirstOrDefault();
                if (eventoActual == null)
                {
                    return Json(new { success = false, message = "El evento especificado no existe." });
                }

                if (eventoActual.Patrocinadores != null && eventoActual.Patrocinadores.Any(p => p.IdPatrocinador == idPatrocinador))
                {
                    return Json(new { success = false, message = "Esta organización ya está vinculada a este evento." });
                }

                // 3. Empujar el objeto completo al sub-array en el documento del Evento
                var filtro = Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, idEvento);
                var actualizacion = Builders<DatosEvento>.Update.Push(e => e.Patrocinadores, patrocinadorGlobal);

                coleccionEventos.UpdateOne(filtro, actualizacion);

                return Json(new { success = true, message = $"¡{patrocinadorGlobal.NombrePatrocinador} se ha vinculado con éxito!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error en MongoDB: " + ex.Message });
            }
        }

        // =============================================
        // MODIFICADO GET AJAX: EliminarPatrocinador (Desvincular)
        // =============================================
        public ActionResult EliminarPatrocinador(int idEvento, int idPatrocinador)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar este evento.";
                    return RedirectToAction("Detalle", new { id = idEvento });
                }

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");

                // Usamos el operador Pull de MongoDB para remover del sub-array por ID directamente
                var filtro = Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, idEvento);
                var actualizacion = Builders<DatosEvento>.Update.PullFilter(e => e.Patrocinadores, p => p.IdPatrocinador == idPatrocinador);

                var resultado = coleccionEventos.UpdateOne(filtro, actualizacion);

                if (resultado.ModifiedCount > 0)
                    TempData["Exito"] = "El patrocinador fue desvinculado de este evento.";
                else
                    TempData["Error"] = "No se pudo realizar la desvinculación.";

                return RedirectToAction("Detalle", new { id = idEvento });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al desvincular el patrocinador: " + ex.Message;
                return RedirectToAction("Detalle", new { id = idEvento });
            }
        }
    }
}