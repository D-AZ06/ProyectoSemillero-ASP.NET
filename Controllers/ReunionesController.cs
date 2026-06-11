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
        // 1. LISTADO Y FILTROS (ACTUALIZADO PARA FILTRAR POR TODO)
        // ==========================================
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            var coleccionReuniones = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
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
            var ultimo = colReuniones.Find(new BsonDocument()).SortByDescending(r => r.IdReunion).FirstOrDefault();

            // AQUÍ APLICAMOS LA SECUENCIA: Si hay registros >= 600, suma 1. Si no hay, arranca en 600.
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

            // 1. Buscamos al líder en la base de datos
            int idLiderActual = (int)Session["IdUsuario"];
            var colUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var liderDb = colUsuarios.Find(u => u.IdUsuario == idLiderActual).FirstOrDefault();

            // 2. AQUÍ ESTÁ LA MAGIA ANTIBALAS PARA EL ERROR DEL INT?
            int semilleroIdSeguro = 0;
            if (liderDb != null && liderDb.IdSemillero != null)
            {
                // Forzamos la conversión a entero puro para que C# no se asuste
                semilleroIdSeguro = Convert.ToInt32(liderDb.IdSemillero);
            }

            // 3. Pasamos los datos seguros al ViewBag para la vista
            ViewBag.IdLider = idLiderActual;
            ViewBag.IdSemillero = semilleroIdSeguro;

            // 4. Armamos el modelo inicializando con los enteros puros
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
                            // Lo guardamos asegurando el nombre de campo principal
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

                // Asignamos el ID directamente sumando 1 al último encontrado
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

            if (reunion == null || (reunion.EstadoReunion != "Programada" && reunion.EstadoReunion != "Reprogramado"))
            {
                TempData["Error"] = "Esta reunión se encuentra en un estado inalterable.";
                return RedirectToAction("Index");
            }

            CargarDatosFormulario();
            return View(reunion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosReunion model)
        {
            string analisisMotivo = ValidarMotivoEstricto(model.MotivoReunion);
            if (analisisMotivo != "OK")
            {
                TempData["Error"] = analisisMotivo;
                CargarDatosFormulario();
                return View(model);
            }

            var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
            coleccion.ReplaceOne(r => r.IdReunion == model.IdReunion, model);

            TempData["Exito"] = "Reunión actualizada exitosamente.";
            return RedirectToAction("Index");
        }

        // ==========================================
        // 4. CANCELAR Y OCUPACIÓN (AJAX)
        // ==========================================
        [HttpPost]
        public JsonResult CancelarReunion(int id)
        {
            var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
            var reunion = coleccion.Find(r => r.IdReunion == id).FirstOrDefault();

            if (reunion == null) return Json(new { success = false, message = "No encontrada." });
            if (reunion.EstadoReunion == "Por iniciar" || reunion.EstadoReunion == "En ejecución" || reunion.EstadoReunion == "Finalizada")
            {
                return Json(new { success = false, message = "No se puede cancelar una reunión en curso o terminada." });
            }

            reunion.EstadoReunion = "Cancelado";
            if (reunion.InvestigadoresConvocados != null)
            {
                foreach (var inv in reunion.InvestigadoresConvocados) inv.EstadoAsistencia = "Conflicto";
            }

            coleccion.ReplaceOne(r => r.IdReunion == id, reunion);
            return Json(new { success = true });
        }

        [HttpGet]
        public JsonResult ObtenerOcupacion(string fecha, string horaInicio, string horaFin, int idIgnorar = 0)
        {
            var coleccion = conexionDB.Database.GetCollection<DatosReunion>("Reuniones");
            var filtro = Builders<DatosReunion>.Filter.And(
                Builders<DatosReunion>.Filter.Eq(r => r.FechaReunion, fecha),
                Builders<DatosReunion>.Filter.Ne(r => r.EstadoReunion, "Cancelado"),
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

                    // Verifica si las horas se cruzan
                    if (inicioReq < finBD && finReq > inicioBD)
                    {
                        lugaresOcupados.Add(r.LugarReunion);
                        if (r.InvestigadoresConvocados != null)
                        {
                            // AQUÍ ESTÁ LA MAGIA: Extraemos el ID sin importar cómo lo llamó MongoDB
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