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

        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            var coleccionReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");

            var todasLasReuniones = coleccionReuniones.Find(new BsonDocument()).ToList();
            DateTime ahora = DateTime.Now;

            foreach (var r in todasLasReuniones)
            {
                if (r.EstadoReunion == "Programada" || r.EstadoReunion == "Reprogramada" || r.EstadoReunion == "En ejecución")
                {
                    if (DateTime.TryParse(r.FechaReunion + " " + r.HoraInicio, out DateTime inicio) &&
                        DateTime.TryParse(r.FechaReunion + " " + r.HoraFin, out DateTime fin))
                    {
                        string estadoCorrecto = r.EstadoReunion;

                        if (ahora > fin)
                        {
                            estadoCorrecto = "Terminada";
                        }
                        else if (ahora >= inicio && ahora <= fin)
                        {
                            estadoCorrecto = "En ejecución";
                        }

                        if (estadoCorrecto != r.EstadoReunion)
                        {
                            r.EstadoReunion = estadoCorrecto;
                            coleccionReuniones.ReplaceOne(x => x.IdReunion == r.IdReunion, r);
                        }
                    }
                }
            }

            var builder = Builders<DatosReunion>.Filter;
            FilterDefinition<DatosReunion> filtroSeguridad = builder.Empty;

            if (rolUsuario == "Líder")
            {
                int idLider = (int)Session["IdUsuario"];
                filtroSeguridad = builder.Eq(r => r.IdLider, idLider);
            }
            else if (rolUsuario == "Investigador" || rolUsuario == "Estudiante")
            {
                int idUsuario = (int)Session["IdUsuario"];
                filtroSeguridad = builder.ElemMatch(r => r.InvestigadoresConvocados, i => i.IdInvestigador == idUsuario);
            }

            FilterDefinition<DatosReunion> filtroBusqueda = builder.Empty;
            if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
            {
                switch (tipoFiltro)
                {
                    case "idReunion":
                        if (int.TryParse(valorFiltro, out int idReu)) filtroBusqueda = builder.Eq(r => r.IdReunion, idReu);
                        break;
                    case "fechaReunion":
                    case "estadoReunion":
                    case "lugarReunion":
                    case "motivoReunion":
                    case "horaInicio":
                    case "horaFin":
                        filtroBusqueda = builder.Regex(tipoFiltro, new BsonRegularExpression(valorFiltro, "i"));
                        break;
                    case "mesReunion":
                        filtroBusqueda = builder.Regex("fechaReunion", new BsonRegularExpression("^" + valorFiltro, "i"));
                        break;
                }
            }

            var filtroFinal = builder.And(filtroSeguridad, filtroBusqueda);
            var lista = coleccionReuniones.Find(filtroFinal).SortByDescending(r => r.FechaReunion).ToList();

            return View(lista);
        }

        private void CargarDatosFormulario()
        {
            var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            ViewBag.ListaInvestigadores = colUsuarios.Find(u => u.RolUsuario == "Investigador").ToList();

            var colReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
            var lugaresUnicos = colReuniones.Distinct<string>("lugarReunion", Builders<DatosReunion>.Filter.Empty).ToList();
            ViewBag.ListaLugaresExistentes = lugaresUnicos;

            var ultimo = colReuniones.Find(new BsonDocument()).SortByDescending(r => r.IdReunion).FirstOrDefault();
            ViewBag.SiguienteIdReunion = (ultimo != null && ultimo.IdReunion >= 600) ? ultimo.IdReunion + 1 : 600;

            int idLiderActual = (int)Session["IdUsuario"];
            var liderDb = colUsuarios.Find(u => u.IdUsuario == idLiderActual).FirstOrDefault();
            ViewBag.NombreLider = liderDb != null ? liderDb.NombreUsuario : "Desconocido";
        }

        [HttpGet]
        public ActionResult Agregar()
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

            CargarDatosFormulario();

            int idLiderActual = (int)Session["IdUsuario"];
            var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var liderDb = colUsuarios.Find(u => u.IdUsuario == idLiderActual).FirstOrDefault();

            int semilleroIdSeguro = 0;
            if (liderDb != null && liderDb.IdSemillero != null)
            {
                semilleroIdSeguro = Convert.ToInt32(liderDb.IdSemillero);
            }

            ViewBag.IdLider = idLiderActual;
            ViewBag.IdSemillero = semilleroIdSeguro;

            var modeloNuevo = new DatosReunion
            {
                IdLider = idLiderActual,
                IdSemillero = semilleroIdSeguro
            };

            return View(modeloNuevo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosReunion model, int[] investigadoresSeleccionados)
        {
            try
            {
                if (model.InvestigadoresConvocados == null) model.InvestigadoresConvocados = new List<InvestigadorConvocado>();

                if (DateTime.TryParse(model.FechaReunion, out DateTime fecha) && fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    TempData["Error"] = "Operación rechazada: No se permiten reuniones en domingo.";
                    CargarDatosFormulario();
                    return View(model);
                }

                string analisisMotivo = ValidarMotivoEstricto(model.MotivoReunion);
                if (analisisMotivo != "OK")
                {
                    TempData["Error"] = analisisMotivo;
                    CargarDatosFormulario();
                    return View(model);
                }

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
                    CargarDatosFormulario();
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.LugarReunion))
                {
                    TempData["Error"] = "Operación rechazada: El lugar de la reunión es estrictamente obligatorio.";
                    CargarDatosFormulario();
                    return View(model);
                }

                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                var ultimo = coleccion.Find(new BsonDocument()).SortByDescending(r => r.IdReunion).FirstOrDefault();

                model.IdReunion = (ultimo != null && ultimo.IdReunion >= 600) ? ultimo.IdReunion + 1 : 600;
                model.EstadoReunion = "Programada";
                model.IdLider = (int)Session["IdUsuario"];

                coleccion.InsertOne(model);
                TempData["Exito"] = "Reunión creada y agendada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico de base de datos: " + ex.Message;
                CargarDatosFormulario();
                return View(model);
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

            CargarDatosFormulario();
            return View(reunion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosReunion model, int[] investigadoresSeleccionados)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.LugarReunion))
                {
                    TempData["Error"] = "Operación rechazada: El lugar de la reunión es estrictamente obligatorio.";
                    CargarDatosFormulario();
                    return View(model);
                }

                if (DateTime.TryParse(model.FechaReunion, out DateTime fecha) && fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    TempData["Error"] = "Operación rechazada: No se permiten reuniones en domingo.";
                    CargarDatosFormulario();
                    return View(model);
                }

                string analisisMotivo = ValidarMotivoEstricto(model.MotivoReunion);
                if (analisisMotivo != "OK")
                {
                    TempData["Error"] = analisisMotivo;
                    CargarDatosFormulario();
                    return View(model);
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
                    CargarDatosFormulario();
                    return View(model);
                }

                var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
                var original = coleccion.Find(r => r.IdReunion == model.IdReunion).FirstOrDefault();

                if (original != null)
                {
                    if (original.FechaReunion != model.FechaReunion ||
                        original.HoraInicio != model.HoraInicio ||
                        original.HoraFin != model.HoraFin)
                    {
                        model.EstadoReunion = "Reprogramada";
                    }
                    else
                    {
                        model.EstadoReunion = original.EstadoReunion;
                    }
                }

                model.IdLider = (int)Session["IdUsuario"];
                coleccion.ReplaceOne(r => r.IdReunion == model.IdReunion, model);

                TempData["Exito"] = "Reunión actualizada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico de base de datos: " + ex.Message;
                CargarDatosFormulario();
                return View(model);
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

                if (DateTime.TryParse(reunion.FechaReunion + " " + reunion.HoraInicio, out DateTime inicioReunion))
                {
                    if (inicioReunion < DateTime.Now)
                    {
                        return Json(new { success = false, message = "No se puede cancelar: La reunión ya comenzó o ya pasó." });
                    }
                }

                if (reunion.EstadoReunion == "Por iniciar" || reunion.EstadoReunion == "En ejecución" || reunion.EstadoReunion == "Terminada")
                {
                    return Json(new { success = false, message = "No se puede cancelar una reunión en curso o terminada." });
                }

                reunion.EstadoReunion = "Cancelada";
                if (reunion.InvestigadoresConvocados != null)
                {
                    foreach (var inv in reunion.InvestigadoresConvocados) inv.EstadoAsistencia = "Conflicto";
                }

                coleccion.ReplaceOne(r => r.IdReunion == id, reunion);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error del servidor: " + ex.Message });
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

            if (TimeSpan.TryParse(horaInicio, out TimeSpan inicioReq) && TimeSpan.TryParse(horaFin, out TimeSpan finReq))
            {
                foreach (var r in reunionesDia)
                {
                    TimeSpan inicioBD = TimeSpan.Parse(r.HoraInicio);
                    TimeSpan finBD = TimeSpan.Parse(r.HoraFin);

                    if (inicioReq < finBD && finReq > inicioBD)
                    {
                        lugaresOcupados.Add(r.LugarReunion);
                        if (r.InvestigadoresConvocados != null)
                        {
                            investigadoresOcupados.AddRange(r.InvestigadoresConvocados.Select(i => i.IdReal));
                        }
                    }
                }
            }
            return Json(new { lugares = lugaresOcupados.Distinct(), investigadores = investigadoresOcupados.Distinct() }, JsonRequestBehavior.AllowGet);
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