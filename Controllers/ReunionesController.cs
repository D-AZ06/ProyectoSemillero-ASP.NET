using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class ReunionesController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // ==========================================
        // ACCIÓN INDEX: TOTALMENTE BLINDADA Y OPTIMIZADA
        // ==========================================
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

            // Trim y OrdinalIgnoreCase garantizan que no fallen minúsculas ni espacios fantasma
            string rolUsuario = Session["Rol"].ToString().Trim();
            int idUsuarioLogueado = (int)Session["IdUsuario"];

            var coleccionReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");

            // 1. Traemos todo UNA SOLA VEZ a memoria para procesar
            var todasLasReuniones = coleccionReuniones.Find(new BsonDocument()).ToList();
            DateTime ahora = DateTime.Now;

            // 2. Actualizamos los estados en vivo
            foreach (var r in todasLasReuniones)
            {
                if (r.EstadoReunion == "Programada" || r.EstadoReunion == "Reprogramada" || r.EstadoReunion == "En ejecución")
                {
                    if (DateTime.TryParse(r.FechaReunion + " " + r.HoraInicio, out DateTime inicio) &&
                        DateTime.TryParse(r.FechaReunion + " " + r.HoraFin, out DateTime fin))
                    {
                        string estadoCorrecto = r.EstadoReunion;

                        if (ahora > fin) estadoCorrecto = "Terminada";
                        else if (ahora >= inicio && ahora <= fin) estadoCorrecto = "En ejecución";

                        if (estadoCorrecto != r.EstadoReunion)
                        {
                            r.EstadoReunion = estadoCorrecto;
                            coleccionReuniones.ReplaceOne(x => x.IdReunion == r.IdReunion, r);
                        }
                    }
                }
            }

            // 3. FILTRADO INFALIBLE EN C# (Ignoramos los mapeos raros de MongoDB)
            IEnumerable<DatosReunion> listaFiltrada = todasLasReuniones;

            // Filtro de Seguridad por Roles
            if (rolUsuario.Equals("Líder", StringComparison.OrdinalIgnoreCase))
            {
                listaFiltrada = listaFiltrada.Where(r => r.IdLider == idUsuarioLogueado);
            }
            else if (rolUsuario.Equals("Investigador", StringComparison.OrdinalIgnoreCase) || rolUsuario.Equals("Estudiante", StringComparison.OrdinalIgnoreCase))
            {
                // Verifica exactamente si el usuario está dentro de la lista de convocados (C# puro es a prueba de balas)
                listaFiltrada = listaFiltrada.Where(r => r.InvestigadoresConvocados != null &&
                                                         r.InvestigadoresConvocados.Any(i => i.IdInvestigador == idUsuarioLogueado));
            }
            else if (rolUsuario.Equals("Administrador", StringComparison.OrdinalIgnoreCase) || rolUsuario.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                // El Admin pasa de largo, ve todo
            }
            else
            {
                // Por seguridad extrema, si el rol es irreconocible, no mostramos nada
                listaFiltrada = listaFiltrada.Where(r => false);
            }

            // 4. Filtro de Búsqueda (Barra de Búsqueda de la Vista)
            if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
            {
                string v = valorFiltro.Trim().ToLower();
                switch (tipoFiltro)
                {
                    case "idReunion":
                        if (int.TryParse(v, out int idReu))
                            listaFiltrada = listaFiltrada.Where(r => r.IdReunion == idReu);
                        break;
                    case "fechaReunion":
                        listaFiltrada = listaFiltrada.Where(r => r.FechaReunion != null && r.FechaReunion.ToLower().Contains(v));
                        break;
                    case "estadoReunion":
                        listaFiltrada = listaFiltrada.Where(r => r.EstadoReunion != null && r.EstadoReunion.ToLower().Contains(v));
                        break;
                    case "lugarReunion":
                        listaFiltrada = listaFiltrada.Where(r => r.LugarReunion != null && r.LugarReunion.ToLower().Contains(v));
                        break;
                    case "motivoReunion":
                        listaFiltrada = listaFiltrada.Where(r => r.MotivoReunion != null && r.MotivoReunion.ToLower().Contains(v));
                        break;
                    case "horaInicio":
                        listaFiltrada = listaFiltrada.Where(r => r.HoraInicio != null && r.HoraInicio.ToLower().Contains(v));
                        break;
                    case "horaFin":
                        listaFiltrada = listaFiltrada.Where(r => r.HoraFin != null && r.HoraFin.ToLower().Contains(v));
                        break;
                    case "mesReunion":
                        listaFiltrada = listaFiltrada.Where(r => r.FechaReunion != null && r.FechaReunion.ToLower().StartsWith(v));
                        break;
                }
            }

            // Ordenamos descendentemente por fecha y mandamos a la vista
            var listaFinal = listaFiltrada.OrderByDescending(r => r.FechaReunion).ToList();

            return View(listaFinal);
        }

        private void CargarDatosFormulario()
        {
            var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var colReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");

            ViewBag.ListaLugaresExistentes = colReuniones.Distinct<string>("lugarReunion", Builders<DatosReunion>.Filter.Empty).ToList();

            var ultimo = colReuniones.Find(new BsonDocument()).SortByDescending(r => r.IdReunion).FirstOrDefault();
            ViewBag.SiguienteIdReunion = (ultimo != null && ultimo.IdReunion >= 600) ? ultimo.IdReunion + 1 : 600;

            string rolUsuario = Session["Rol"].ToString();
            ViewBag.RolUsuario = rolUsuario;

            if (rolUsuario == "Administrador" || rolUsuario == "Admin")
            {
                var colSemilleros = conexionDB.Database.GetCollection<BsonDocument>("Semilleros");
                var listaSemilleros = colSemilleros.Find(new BsonDocument()).ToList()
                            .Select(s => new { IdSemillero = s["idSemillero"].AsInt32, NombreSemillero = s["nombreSemillero"].AsString }).ToList();

                ViewBag.ListaSemilleros = new SelectList(listaSemilleros, "IdSemillero", "NombreSemillero");
                ViewBag.ListaInvestigadores = new List<DatosUsuario>();
                ViewBag.IdLider = null;
                ViewBag.IdSemillero = null;
                ViewBag.NombreLider = "";
            }
            else
            {
                int idLiderActual = (int)Session["IdUsuario"];
                var liderDb = colUsuarios.Find(u => u.IdUsuario == idLiderActual).FirstOrDefault();
                int idSemilleroLider = liderDb != null && liderDb.IdSemillero.HasValue ? liderDb.IdSemillero.Value : 0;

                var colSemilleros = conexionDB.Database.GetCollection<BsonDocument>("Semilleros");
                var semDB = colSemilleros.Find(Builders<BsonDocument>.Filter.Eq("idSemillero", idSemilleroLider)).FirstOrDefault();
                ViewBag.NombreSemilleroLider = semDB != null ? semDB["nombreSemillero"].AsString : "Desconocido";

                ViewBag.IdLider = idLiderActual;
                ViewBag.IdSemillero = idSemilleroLider;

                var lideres = colUsuarios.Find(u => u.IdSemillero == idSemilleroLider && u.RolUsuario == "Líder").ToList();
                ViewBag.ListaLideres = new SelectList(lideres, "IdUsuario", "NombreUsuario", idLiderActual);

                ViewBag.ListaInvestigadores = colUsuarios.Find(u => u.IdSemillero == idSemilleroLider && u.RolUsuario == "Investigador").ToList();
            }
        }

        private void CargarDatosModificar(DatosReunion reunion)
        {
            CargarDatosFormulario();
            var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var colSemilleros = conexionDB.Database.GetCollection<BsonDocument>("Semilleros");

            var semDB = colSemilleros.Find(Builders<BsonDocument>.Filter.Eq("idSemillero", reunion.IdSemillero)).FirstOrDefault();
            ViewBag.NombreSemilleroReunion = semDB != null ? semDB["nombreSemillero"].AsString : "Desconocido";

            var lideres = colUsuarios.Find(u => u.IdSemillero == reunion.IdSemillero && u.RolUsuario == "Líder").ToList();
            ViewBag.ListaLideres = new SelectList(lideres, "IdUsuario", "NombreUsuario", reunion.IdLider);

            ViewBag.ListaInvestigadores = colUsuarios.Find(u => u.IdSemillero == reunion.IdSemillero && (u.RolUsuario == "Investigador" || u.RolUsuario == "Estudiante")).ToList();
        }

        [HttpGet]
        public JsonResult ObtenerLideresYInvestigadores(int idSemillero)
        {
            var colUsr = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

            var lideres = colUsr.Find(u => u.IdSemillero == idSemillero && u.RolUsuario == "Líder").ToList()
                .Select(u => new { id = u.IdUsuario, nombre = u.NombreUsuario }).ToList();

            var investigadores = colUsr.Find(u => u.IdSemillero == idSemillero && (u.RolUsuario == "Investigador" || u.RolUsuario == "Estudiante")).ToList()
                .Select(u => new { id = u.IdUsuario, nombre = u.NombreUsuario }).ToList();

            return Json(new { lideres = lideres, investigadores = investigadores }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Agregar()
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

            CargarDatosFormulario();

            var modeloNuevo = new DatosReunion();
            if (Session["Rol"].ToString() == "Líder")
            {
                modeloNuevo.IdLider = ViewBag.IdLider;
                modeloNuevo.IdSemillero = ViewBag.IdSemillero;
            }

            return View(modeloNuevo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosReunion model, int[] investigadoresSeleccionados)
        {
            try
            {
                if (DateTime.TryParse(model.FechaReunion + " " + model.HoraInicio, out DateTime inicioReunion))
                {
                    if (inicioReunion < DateTime.Now.AddMinutes(55))
                    {
                        TempData["Error"] = "Operación rechazada: La reunión debe agendarse con al menos 1 hora de anticipación.";
                        CargarDatosFormulario(); return View(model);
                    }
                }

                if (TimeSpan.TryParse(model.HoraFin, out TimeSpan horaFinLimite))
                {
                    if (horaFinLimite > new TimeSpan(22, 0, 0))
                    {
                        TempData["Error"] = "Operación rechazada: Las reuniones no pueden extenderse más allá de las 10:00 p.m.";
                        CargarDatosFormulario(); return View(model);
                    }
                }

                if (DateTime.TryParse(model.FechaReunion, out DateTime fecha) && fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    TempData["Error"] = "Operación rechazada: No se permiten reuniones en domingo.";
                    CargarDatosFormulario(); return View(model);
                }

                string analisisMotivo = ValidarMotivoEstricto(model.MotivoReunion);
                if (analisisMotivo != "OK")
                {
                    TempData["Error"] = analisisMotivo;
                    CargarDatosFormulario(); return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.LugarReunion))
                {
                    TempData["Error"] = "Operación rechazada: El lugar de la reunión es estrictamente obligatorio.";
                    CargarDatosFormulario(); return View(model);
                }

                if (Session["Rol"].ToString() == "Líder") model.IdLider = (int)Session["IdUsuario"];

                if (TimeSpan.TryParse(model.HoraInicio, out TimeSpan tInicio) && TimeSpan.TryParse(model.HoraFin, out TimeSpan tFin))
                {
                    var colReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                    var reunionesConflicto = colReuniones.Find(r => r.FechaReunion == model.FechaReunion && r.EstadoReunion != "Cancelada" && r.IdReunion != model.IdReunion).ToList();

                    foreach (var r in reunionesConflicto)
                    {
                        if (TimeSpan.TryParse(r.HoraInicio, out TimeSpan bdInicio) && TimeSpan.TryParse(r.HoraFin, out TimeSpan bdFin))
                        {
                            if (tInicio < bdFin && tFin > bdInicio)
                            {
                                if (model.LugarReunion.Equals(r.LugarReunion, StringComparison.OrdinalIgnoreCase))
                                {
                                    TempData["Error"] = "Operación rechazada: El lugar ya se encuentra reservado en ese horario.";
                                    CargarDatosFormulario(); return View(model);
                                }

                                if (model.IdLider == r.IdLider)
                                {
                                    TempData["Error"] = "Operación rechazada: El Líder seleccionado ya tiene otra reunión programada en este horario.";
                                    CargarDatosFormulario(); return View(model);
                                }

                                if (investigadoresSeleccionados != null)
                                {
                                    foreach (var idInv in investigadoresSeleccionados)
                                    {
                                        if (r.InvestigadoresConvocados != null && r.InvestigadoresConvocados.Any(i => i.IdInvestigador == idInv))
                                        {
                                            TempData["Error"] = $"Operación rechazada: El investigador con ID {idInv} ya está ocupado en este horario.";
                                            CargarDatosFormulario(); return View(model);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (model.InvestigadoresConvocados == null) model.InvestigadoresConvocados = new List<InvestigadorConvocado>();

                if (investigadoresSeleccionados != null && investigadoresSeleccionados.Length > 0)
                {
                    var colUsr = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                    foreach (int idInv in investigadoresSeleccionados)
                    {
                        var usr = colUsr.Find(u => u.IdUsuario == idInv).FirstOrDefault();
                        if (usr != null)
                        {
                            model.InvestigadoresConvocados.Add(new InvestigadorConvocado { IdInvestigador = usr.IdUsuario, Nombre = usr.NombreUsuario, EstadoAsistencia = "Pendiente" });
                        }
                    }
                }
                else
                {
                    TempData["Error"] = "Debe convocar al menos a un investigador.";
                    CargarDatosFormulario(); return View(model);
                }

                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                var ultimo = coleccion.Find(new BsonDocument()).SortByDescending(r => r.IdReunion).FirstOrDefault();

                model.IdReunion = (ultimo != null && ultimo.IdReunion >= 600) ? ultimo.IdReunion + 1 : 600;
                model.EstadoReunion = "Programada";

                coleccion.InsertOne(model);
                TempData["Exito"] = "Reunión creada y agendada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico de base de datos: " + ex.Message;
                CargarDatosFormulario(); return View(model);
            }
        }

        [HttpGet]
        public ActionResult Modificar(int? id)
        {
            if (id == null) return RedirectToAction("Index");

            var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
            var reunion = coleccion.Find(r => r.IdReunion == id).FirstOrDefault();

            if (reunion == null || (reunion.EstadoReunion != "Programada" && reunion.EstadoReunion != "Reprogramada"))
            {
                TempData["Error"] = "Esta reunión se encuentra en un estado inalterable.";
                return RedirectToAction("Index");
            }

            CargarDatosModificar(reunion);
            return View(reunion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosReunion model, int[] investigadoresSeleccionados)
        {
            try
            {
                if (DateTime.TryParse(model.FechaReunion + " " + model.HoraInicio, out DateTime inicioReunion))
                {
                    if (inicioReunion < DateTime.Now.AddMinutes(55))
                    {
                        TempData["Error"] = "Operación rechazada: La reunión debe agendarse con al menos 1 hora de anticipación.";
                        CargarDatosModificar(model); return View(model);
                    }
                }

                if (TimeSpan.TryParse(model.HoraFin, out TimeSpan horaFinLimite))
                {
                    if (horaFinLimite > new TimeSpan(22, 0, 0))
                    {
                        TempData["Error"] = "Operación rechazada: Las reuniones no pueden extenderse más allá de las 10:00 p.m.";
                        CargarDatosModificar(model); return View(model);
                    }
                }

                if (string.IsNullOrWhiteSpace(model.LugarReunion))
                {
                    TempData["Error"] = "Operación rechazada: El lugar de la reunión es estrictamente obligatorio.";
                    CargarDatosModificar(model); return View(model);
                }

                if (DateTime.TryParse(model.FechaReunion, out DateTime fecha) && fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    TempData["Error"] = "Operación rechazada: No se permiten reuniones en domingo.";
                    CargarDatosModificar(model); return View(model);
                }

                string analisisMotivo = ValidarMotivoEstricto(model.MotivoReunion);
                if (analisisMotivo != "OK")
                {
                    TempData["Error"] = analisisMotivo;
                    CargarDatosModificar(model); return View(model);
                }

                if (Session["Rol"].ToString() == "Líder") model.IdLider = (int)Session["IdUsuario"];

                if (TimeSpan.TryParse(model.HoraInicio, out TimeSpan tInicio) && TimeSpan.TryParse(model.HoraFin, out TimeSpan tFin))
                {
                    var colReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                    var reunionesConflicto = colReuniones.Find(r => r.FechaReunion == model.FechaReunion && r.EstadoReunion != "Cancelada" && r.IdReunion != model.IdReunion).ToList();

                    foreach (var r in reunionesConflicto)
                    {
                        if (TimeSpan.TryParse(r.HoraInicio, out TimeSpan bdInicio) && TimeSpan.TryParse(r.HoraFin, out TimeSpan bdFin))
                        {
                            if (tInicio < bdFin && tFin > bdInicio)
                            {
                                if (model.LugarReunion.Equals(r.LugarReunion, StringComparison.OrdinalIgnoreCase))
                                {
                                    TempData["Error"] = "Operación rechazada: El lugar ya se encuentra reservado en ese horario.";
                                    CargarDatosModificar(model); return View(model);
                                }

                                if (model.IdLider == r.IdLider)
                                {
                                    TempData["Error"] = "Operación rechazada: El Líder seleccionado ya tiene otra reunión programada en este horario.";
                                    CargarDatosModificar(model); return View(model);
                                }

                                if (investigadoresSeleccionados != null)
                                {
                                    foreach (var idInv in investigadoresSeleccionados)
                                    {
                                        if (r.InvestigadoresConvocados != null && r.InvestigadoresConvocados.Any(i => i.IdInvestigador == idInv))
                                        {
                                            TempData["Error"] = $"Operación rechazada: El investigador con ID {idInv} ya está ocupado en este horario.";
                                            CargarDatosModificar(model); return View(model);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                model.InvestigadoresConvocados = new List<InvestigadorConvocado>();
                if (investigadoresSeleccionados != null && investigadoresSeleccionados.Length > 0)
                {
                    var colUsr = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                    foreach (int idInv in investigadoresSeleccionados)
                    {
                        var usr = colUsr.Find(u => u.IdUsuario == idInv).FirstOrDefault();
                        if (usr != null)
                        {
                            model.InvestigadoresConvocados.Add(new InvestigadorConvocado { IdInvestigador = usr.IdUsuario, Nombre = usr.NombreUsuario, EstadoAsistencia = "Pendiente" });
                        }
                    }
                }
                else
                {
                    TempData["Error"] = "Debe convocar al menos a un investigador.";
                    CargarDatosModificar(model); return View(model);
                }

                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                var original = coleccion.Find(r => r.IdReunion == model.IdReunion).FirstOrDefault();

                if (original != null)
                {
                    if (original.FechaReunion != model.FechaReunion || original.HoraInicio != model.HoraInicio || original.HoraFin != model.HoraFin)
                    {
                        model.EstadoReunion = "Reprogramada";
                    }
                    else
                    {
                        model.EstadoReunion = original.EstadoReunion;
                    }
                }

                coleccion.ReplaceOne(r => r.IdReunion == model.IdReunion, model);

                TempData["Exito"] = "Reunión actualizada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico de base de datos: " + ex.Message;
                CargarDatosModificar(model); return View(model);
            }
        }

        [HttpPost]
        public JsonResult CancelarReunion(int id)
        {
            try
            {
                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                var reunion = coleccion.Find(r => r.IdReunion == id).FirstOrDefault();

                if (reunion == null) return Json(new { success = false, message = "La reunión no existe en la BD." });

                if (reunion.InvestigadoresConvocados != null && reunion.InvestigadoresConvocados.Any(i => i.EstadoAsistencia == "Confirmada"))
                {
                    return Json(new { success = false, message = "Operación denegada: No puedes cancelar porque ya hay investigadores que confirmaron su asistencia." });
                }

                if (DateTime.TryParse(reunion.FechaReunion + " " + reunion.HoraInicio, out DateTime inicioReunion))
                {
                    if (inicioReunion < DateTime.Now) return Json(new { success = false, message = "No se puede cancelar: La reunión ya comenzó o ya pasó." });
                }

                if (reunion.EstadoReunion == "Por iniciar" || reunion.EstadoReunion == "En ejecución" || reunion.EstadoReunion == "Terminada")
                {
                    return Json(new { success = false, message = "No se puede cancelar una reunión en curso o terminada." });
                }

                reunion.EstadoReunion = "Cancelada";
                if (reunion.InvestigadoresConvocados != null)
                {
                    foreach (var inv in reunion.InvestigadoresConvocados) inv.EstadoAsistencia = "Pendiente";
                }

                coleccion.ReplaceOne(r => r.IdReunion == id, reunion);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error del servidor: " + ex.Message });
            }
        }

        public ActionResult Eliminar(int id)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario != "Administrador" && rolUsuario != "Admin")
            {
                TempData["Error"] = "Solo los Administradores tienen permiso para eliminar reuniones permanentemente.";
                return RedirectToAction("Index");
            }

            try
            {
                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");

                var reunionExiste = coleccion.Find(r => r.IdReunion == id).FirstOrDefault();
                if (reunionExiste == null)
                {
                    TempData["Error"] = $"No se encontró la reunión con el ID {id} en la base de datos.";
                    return RedirectToAction("Index");
                }

                var resultado = coleccion.DeleteOne(r => r.IdReunion == id);

                if (resultado.DeletedCount > 0)
                {
                    TempData["Exito"] = $"La reunión {id} ha sido eliminada permanentemente.";
                }
                else
                {
                    TempData["Error"] = "Hubo un problema y no se pudo borrar el registro.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error del sistema al eliminar: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public JsonResult ResponderAsistencia(int idReunion, string respuesta)
        {
            try
            {
                if (Session["IdUsuario"] == null) return Json(new { success = false, message = "Sesión expirada." });
                int idUsuarioLogueado = (int)Session["IdUsuario"];

                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                var reunion = coleccion.Find(r => r.IdReunion == idReunion).FirstOrDefault();

                if (reunion == null) return Json(new { success = false, message = "La reunión no existe en la BD." });

                if (reunion.EstadoReunion == "Cancelada" || reunion.EstadoReunion == "Terminada" || reunion.EstadoReunion == "En ejecución")
                {
                    return Json(new { success = false, message = "No puedes cambiar tu asistencia a una reunión en este estado." });
                }

                var investigador = reunion.InvestigadoresConvocados?.FirstOrDefault(i => i.IdInvestigador == idUsuarioLogueado);
                if (investigador == null) return Json(new { success = false, message = "No estás convocado a esta reunión." });

                investigador.EstadoAsistencia = respuesta;
                coleccion.ReplaceOne(r => r.IdReunion == idReunion, reunion);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult ObtenerOcupacion(string fecha, string horaInicio, string horaFin, int idIgnorar = 0)
        {
            var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
            var filtro = Builders<DatosReunion>.Filter.And(
                Builders<DatosReunion>.Filter.Eq(r => r.FechaReunion, fecha),
                Builders<DatosReunion>.Filter.Ne(r => r.EstadoReunion, "Cancelada"),
                Builders<DatosReunion>.Filter.Ne(r => r.IdReunion, idIgnorar)
            );

            var reunionesDia = coleccion.Find(filtro).ToList();
            List<string> lugaresOcupados = new List<string>();
            List<int> investigadoresOcupados = new List<int>();
            List<int> lideresOcupados = new List<int>();

            if (TimeSpan.TryParse(horaInicio, out TimeSpan inicioReq) && TimeSpan.TryParse(horaFin, out TimeSpan finReq))
            {
                foreach (var r in reunionesDia)
                {
                    if (TimeSpan.TryParse(r.HoraInicio, out TimeSpan inicioBD) && TimeSpan.TryParse(r.HoraFin, out TimeSpan finBD))
                    {
                        if (inicioReq < finBD && finReq > inicioBD)
                        {
                            lugaresOcupados.Add(r.LugarReunion);

                            if (r.IdLider != null) lideresOcupados.Add((int)r.IdLider);

                            if (r.InvestigadoresConvocados != null)
                            {
                                investigadoresOcupados.AddRange(r.InvestigadoresConvocados
                                    .Where(i => i.IdInvestigador.HasValue)
                                    .Select(i => i.IdInvestigador.Value));
                            }
                        }
                    }
                }
            }
            return Json(new { lugares = lugaresOcupados.Distinct(), investigadores = investigadoresOcupados.Distinct(), lideres = lideresOcupados.Distinct() }, JsonRequestBehavior.AllowGet);
        }

        private string ValidarMotivoEstricto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 10) return "Motivo inválido: Mínimo 10 caracteres.";
            if (texto.Length > 500) return "Motivo inválido: Máximo 500 caracteres.";
            int vocales = texto.Count(c => "aeiouAEIOUáéíóúÁÉÍÓÚ".Contains(c));
            int letras = texto.Count(char.IsLetter);
            if (letras > 0 && (double)vocales / letras < 0.20) return "Motivo inválido: Texto incoherente (menos del 20% de vocales).";
            if (Regex.IsMatch(texto, @"(.)\1{3,}")) return "Motivo inválido: Contiene 4 o más caracteres repetidos seguidos.";
            return "OK";
        }
    }
}