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
        // HELPER PRIVADO: calcula el estado automático
        // =============================================
        private string CalcularEstadoEvento(DatosEvento evento)
        {
            // Si ya está cancelado, no se toca
            if (evento.Estado == "Cancelado") return "Cancelado";

            if (!DateTime.TryParse(evento.FechaEvento, out DateTime fechaEvento))
                return evento.Estado;

            DateTime ahora = DateTime.Now;
            DateTime fechaSolo = fechaEvento.Date;

            // Valores por defecto si no hay hora definida
            TimeSpan horaInicio = TimeSpan.Zero;
            TimeSpan horaFin = new TimeSpan(23, 59, 0);

            TimeSpan.TryParse(evento.HoraInicio, out horaInicio);
            TimeSpan.TryParse(evento.HoraFin, out horaFin);

            DateTime inicioEvento = fechaSolo + horaInicio;
            DateTime finEvento = fechaSolo + horaFin;

            if (ahora >= inicioEvento && ahora <= finEvento)
                return "En Ejecución";

            if (ahora > finEvento)
                return "Finalizado";

            // Si no aplica ninguna condición temporal, devuelve el estado que traiga
            return evento.Estado;
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
                        filtroSeguridad = builder.AnyEq(e => e.IdSemilleros, idSemillero);
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

                // ── Actualizar estados automáticamente al listar ──
                foreach (var evento in lista.Where(e => e.Estado != "Cancelado"))
                {
                    string nuevoEstado = CalcularEstadoEvento(evento);
                    if (nuevoEstado != evento.Estado)
                    {
                        var filtroUpdate = Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, evento.IdEvento);
                        var update = Builders<DatosEvento>.Update.Set(e => e.Estado, nuevoEstado);
                        coleccionEventos.UpdateOne(filtroUpdate, update);
                        evento.Estado = nuevoEstado; // actualiza en memoria para la vista
                    }
                }

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

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var colSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                var colProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var colPatrocinadores = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");

                var ultimoEvento = coleccionEventos.Find(Builders<DatosEvento>.Filter.Empty)
                                                   .SortByDescending(e => e.IdEvento)
                                                   .FirstOrDefault();

                var nuevoEvento = new DatosEvento();

                if (ultimoEvento != null && ultimoEvento.IdEvento >= 700)
                    nuevoEvento.IdEvento = ultimoEvento.IdEvento + 1;
                else
                    nuevoEvento.IdEvento = 700;

                // Estado inicial siempre "Programado" al abrir el formulario
                nuevoEvento.Estado = "Programado";
                nuevoEvento.Modalidad = "Presencial";
                nuevoEvento.RequiereInscripcion = false;
                nuevoEvento.CapacidadMaxima = 0;
                nuevoEvento.Agenda = new List<ItemAgenda>();
                nuevoEvento.IdSemilleros = new List<int>();

                if (rolUsuario == "Líder")
                {
                    int idSemillero = (int)Session["IdSemillero"];
                    ViewBag.IdSemilleroFijo = idSemillero;
                    var semillero = colSemilleros.Find(s => s.IdSemillero == idSemillero).FirstOrDefault();
                    ViewBag.NombreSemilleroFijo = semillero?.nombreSemillero ?? "Tu semillero";
                    ViewBag.ListaProyectos = colProyectos.Find(p => p.IdSemillero == idSemillero).ToList();
                }
                else
                {
                    var semilleros = colSemilleros.Find(_ => true).ToList();
                    ViewBag.ListaSemilleros = semilleros;
                    ViewBag.ListaProyectos = colProyectos.Find(_ => true).ToList();
                }

                ViewBag.CatalogoPatrocinadores = colPatrocinadores.Find(_ => true).ToList();

                var todosEventos = coleccionEventos.Find(_ => true).ToList();
                ViewBag.NombresSugeridos = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.NombreEvento)).Select(e => e.NombreEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
                ViewBag.TiposSugeridos = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.TipoEvento)).Select(e => e.TipoEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
                ViewBag.LugaresSugeridos = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.LugarEvento)).Select(e => e.LugarEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(l => l).ToList();

                try
                {
                    var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                    var usuarios = colUsuarios.Find(_ => true).ToList();
                    var listaOrganizadores = usuarios.Select(u => new SelectListItem
                    {
                        Value = u.NombreUsuario,
                        Text = u.NombreUsuario
                    }).ToList();
                    ViewBag.ListaOrganizadores = new SelectList(listaOrganizadores, "Value", "Text");
                }
                catch
                {
                    var listaFallback = coleccionEventos.Find(_ => true).ToList()
                        .Where(e => !string.IsNullOrWhiteSpace(e.OrganizadorEvento))
                        .Select(e => e.OrganizadorEvento.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(o => o).ToList();
                    ViewBag.ListaOrganizadores = new SelectList(listaFallback);
                }

                ViewBag.RolUsuario = rolUsuario;
                return View(nuevoEvento);
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
        public ActionResult Agregar(DatosEvento nuevoEvento, int[] proyectosSeleccionados, int[] patrocinadoresSeleccionados, int[] semillerosSeleccionados)
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

                if (nuevoEvento.Agenda == null)
                    nuevoEvento.Agenda = new List<ItemAgenda>();

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");

                // Asignar lista de semilleros
                if (rolUsuario == "Líder")
                    nuevoEvento.IdSemilleros = new List<int> { (int)Session["IdSemillero"] };
                else
                    nuevoEvento.IdSemilleros = semillerosSeleccionados != null ? semillerosSeleccionados.ToList() : new List<int>();

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

                nuevoEvento.Patrocinadores = new List<DatosPatrocinador>();
                if (patrocinadoresSeleccionados != null && patrocinadoresSeleccionados.Length > 0)
                {
                    var colPatrocinadores = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");
                    foreach (int idPat in patrocinadoresSeleccionados)
                    {
                        var pat = colPatrocinadores.Find(p => p.IdPatrocinador == idPat).FirstOrDefault();
                        if (pat != null)
                            nuevoEvento.Patrocinadores.Add(pat);
                    }
                }

                var existeDuplicado = coleccionEventos.Find(e => e.IdEvento == nuevoEvento.IdEvento).Any();
                if (existeDuplicado)
                {
                    var ultimo = coleccionEventos.Find(Builders<DatosEvento>.Filter.Empty)
                                                 .SortByDescending(e => e.IdEvento)
                                                 .FirstOrDefault();
                    nuevoEvento.IdEvento = (ultimo != null && ultimo.IdEvento >= 700) ? ultimo.IdEvento + 1 : 700;
                }

                // ── Estado automático al crear ──
                // Base: Programado. Pero si registran un evento con fecha ya pasada o en curso, se ajusta.
                nuevoEvento.Estado = "Programado";
                nuevoEvento.Estado = CalcularEstadoEvento(nuevoEvento);

                coleccionEventos.InsertOne(nuevoEvento);
                TempData["Exito"] = $"Evento '{nuevoEvento.NombreEvento}' registrado con ID: {nuevoEvento.IdEvento} — Estado: {nuevoEvento.Estado}";
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
                var colSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                var colProyectos = conexionDB.Database.GetCollection<DatosProyecto>("Proyectos");
                var colPatrocinadores = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");

                var evento = coleccionEventos.Find(e => e.IdEvento == id).FirstOrDefault();

                if (evento == null)
                {
                    TempData["Error"] = "El evento solicitado no existe.";
                    return RedirectToAction("Index");
                }

                if (evento.IdSemilleros == null) evento.IdSemilleros = new List<int>();

                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    if (!evento.IdSemilleros.Contains(idSemilleroLider))
                    {
                        TempData["Error"] = "Acceso denegado: Tu semillero no participa en este evento.";
                        return RedirectToAction("Index");
                    }
                    ViewBag.IdSemilleroFijo = idSemilleroLider;
                    var semillero = colSemilleros.Find(s => s.IdSemillero == idSemilleroLider).FirstOrDefault();
                    ViewBag.NombreSemilleroFijo = semillero?.nombreSemillero ?? "Tu semillero";
                    ViewBag.ListaProyectos = colProyectos.Find(p => p.IdSemillero == idSemilleroLider).ToList();
                }
                else
                {
                    var semilleros = colSemilleros.Find(_ => true).ToList();
                    ViewBag.ListaSemilleros = semilleros;
                    ViewBag.ListaProyectos = colProyectos.Find(_ => true).ToList();
                }

                ViewBag.CatalogoPatrocinadores = colPatrocinadores.Find(_ => true).ToList();

                var todosEventos = coleccionEventos.Find(_ => true).ToList();
                ViewBag.NombresSugeridos = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.NombreEvento)).Select(e => e.NombreEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
                ViewBag.TiposSugeridos = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.TipoEvento)).Select(e => e.TipoEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
                ViewBag.LugaresSugeridos = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.LugarEvento)).Select(e => e.LugarEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(l => l).ToList();

                try
                {
                    var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                    var listaNombresUsuarios = colUsuarios.Find(_ => true).ToList().Select(u => u.NombreUsuario).OrderBy(n => n).ToList();
                    ViewBag.ListaOrganizadores = new SelectList(listaNombresUsuarios);
                }
                catch
                {
                    var listaFallback = todosEventos.Where(e => !string.IsNullOrWhiteSpace(e.OrganizadorEvento)).Select(e => e.OrganizadorEvento.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(o => o).ToList();
                    ViewBag.ListaOrganizadores = new SelectList(listaFallback);
                }

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
        public ActionResult Modificar(DatosEvento eventoModificado, int[] proyectosSeleccionados, int[] patrocinadoresSeleccionados, int[] semillerosSeleccionados)
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

                if (eventoModificado.Agenda == null)
                    eventoModificado.Agenda = new List<ItemAgenda>();

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var filtro = Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, eventoModificado.IdEvento);

                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    filtro = Builders<DatosEvento>.Filter.And(
                        Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, eventoModificado.IdEvento),
                        Builders<DatosEvento>.Filter.AnyEq(e => e.IdSemilleros, idSemilleroLider)
                    );
                }

                var eventoOriginal = coleccionEventos.Find(filtro).FirstOrDefault();

                if (eventoOriginal == null)
                {
                    TempData["Error"] = "No se encontró el evento o no tienes permisos.";
                    return RedirectToAction("Index");
                }

                eventoModificado.Id = eventoOriginal.Id;

                if (rolUsuario == "Líder")
                    eventoModificado.IdSemilleros = eventoOriginal.IdSemilleros;
                else
                    eventoModificado.IdSemilleros = semillerosSeleccionados != null ? semillerosSeleccionados.ToList() : new List<int>();

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

                eventoModificado.Patrocinadores = new List<DatosPatrocinador>();
                if (patrocinadoresSeleccionados != null && patrocinadoresSeleccionados.Length > 0)
                {
                    var colPatrocinadores = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");
                    foreach (int idPat in patrocinadoresSeleccionados)
                    {
                        var pat = colPatrocinadores.Find(p => p.IdPatrocinador == idPat).FirstOrDefault();
                        if (pat != null)
                            eventoModificado.Patrocinadores.Add(pat);
                    }
                }

                // ── Estado automático al modificar ──
                // Si el evento no estaba cancelado, la base al modificar es "Reprogramado".
                // Pero si la fecha/hora ya puso el evento en curso o terminado, prevalece eso.
                if (eventoOriginal.Estado != "Cancelado")
                {
                    eventoModificado.Estado = "Reprogramado";
                    eventoModificado.Estado = CalcularEstadoEvento(eventoModificado);
                }
                else
                {
                    // Si estaba cancelado y lo editan, se mantiene cancelado
                    eventoModificado.Estado = "Cancelado";
                }

                coleccionEventos.ReplaceOne(filtro, eventoModificado);
                TempData["Exito"] = $"El evento ha sido actualizado correctamente — Estado: {eventoModificado.Estado}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar las modificaciones: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // GET: Eventos/Eliminar/id  →  Cancelación lógica
        // =============================================
        public ActionResult Eliminar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para cancelar eventos.";
                    return RedirectToAction("Index");
                }

                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");
                var evento = coleccionEventos.Find(e => e.IdEvento == id).FirstOrDefault();

                if (evento == null)
                {
                    TempData["Error"] = "El evento no existe.";
                    return RedirectToAction("Index");
                }

                // Verificar permisos por semillero para Líder
                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    if (evento.IdSemilleros == null || !evento.IdSemilleros.Contains(idSemilleroLider))
                    {
                        TempData["Error"] = "No tienes permisos para cancelar este evento.";
                        return RedirectToAction("Index");
                    }
                }

                // ── Cancelación lógica: solo cambia el estado a "Cancelado", NO borra el documento ──
                var filtro = Builders<DatosEvento>.Filter.Eq(e => e.IdEvento, id);
                var actualizacion = Builders<DatosEvento>.Update.Set(e => e.Estado, "Cancelado");
                var resultado = coleccionEventos.UpdateOne(filtro, actualizacion);

                if (resultado.ModifiedCount > 0)
                    TempData["Exito"] = $"El evento '{evento.NombreEvento}' ha sido cancelado correctamente.";
                else
                    TempData["Error"] = "No se pudo cancelar el evento.";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cancelar: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // POST: VincularPatrocinadorExistente
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

                var patrocinadorGlobal = coleccionPatrocinadores.Find(p => p.IdPatrocinador == idPatrocinador).FirstOrDefault();
                if (patrocinadorGlobal == null)
                    return Json(new { success = false, message = "El patrocinador seleccionado no existe en el catálogo global." });

                var eventoActual = coleccionEventos.Find(e => e.IdEvento == idEvento).FirstOrDefault();
                if (eventoActual == null)
                    return Json(new { success = false, message = "El evento especificado no existe." });

                if (eventoActual.Patrocinadores != null && eventoActual.Patrocinadores.Any(p => p.IdPatrocinador == idPatrocinador))
                    return Json(new { success = false, message = "Esta organización ya está vinculada a este evento." });

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
        // GET AJAX: EliminarPatrocinador (Desvincular)
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

        // =============================================
        // GET: VerificarLugarOcupado (Validación de cruces)
        // =============================================
        [HttpGet]
        public ActionResult VerificarLugarOcupado(string lugar, string fecha, string horaInicio, string horaFin)
        {
            try
            {
                var coleccionEventos = conexionDB.Database.GetCollection<DatosEvento>("Eventos");

                var eventosPosibles = coleccionEventos.Find(e =>
                    e.LugarEvento == lugar &&
                    e.FechaEvento == fecha &&
                    e.Estado != "Cancelado").ToList();

                bool existeCruce = false;

                if (eventosPosibles.Any())
                {
                    TimeSpan tInicioNuevo = TimeSpan.Parse(horaInicio);
                    TimeSpan tFinNuevo = TimeSpan.Parse(horaFin);

                    foreach (var ev in eventosPosibles)
                    {
                        if (TimeSpan.TryParse(ev.HoraInicio, out TimeSpan tInicioExistente) &&
                            TimeSpan.TryParse(ev.HoraFin, out TimeSpan tFinExistente))
                        {
                            if (tInicioNuevo < tFinExistente && tFinNuevo > tInicioExistente)
                            {
                                existeCruce = true;
                                break;
                            }
                        }
                    }
                }

                return Json(new { ocupado = existeCruce }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception)
            {
                return Json(new { ocupado = false }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}