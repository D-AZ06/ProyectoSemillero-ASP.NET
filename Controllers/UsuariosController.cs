using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Text.RegularExpressions;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class UsuariosController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // GET: Usuarios
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

                var coleccionSemilleros = conexionDB.Database.GetCollection<BsonDocument>("Semilleros");
                var listaSemilleros = coleccionSemilleros.Find(new BsonDocument()).ToList()
                            .Select(s => new { IdSemillero = s["idSemillero"].AsInt32, NombreSemillero = s["nombreSemillero"].AsString }).ToList();
                ViewBag.DiccionarioSemilleros = listaSemilleros.ToDictionary(s => s.IdSemillero, s => s.NombreSemillero);

                var builder = Builders<DatosUsuario>.Filter;
                FilterDefinition<DatosUsuario> filtroSeguridad;

                if (rolUsuario == "Administrador" || rolUsuario == "Admin")
                {
                    filtroSeguridad = builder.Empty;
                }
                else
                {
                    if (Session["IdSemillero"] != null)
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        filtroSeguridad = builder.Eq(u => u.IdSemillero, idSemillero);
                    }
                    else
                    {
                        TempData["Error"] = "Tu usuario no tiene un semillero asignado correctamente.";
                        return View(new List<DatosUsuario>());
                    }
                }

                FilterDefinition<DatosUsuario> filtroBusqueda = builder.Empty;

                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    switch (tipoFiltro)
                    {
                        case "idUsuario":
                            if (int.TryParse(valorFiltro, out int idUsu)) filtroBusqueda = builder.Eq(u => u.IdUsuario, idUsu);
                            break;
                        case "nombreUsuario":
                            filtroBusqueda = builder.Regex(u => u.NombreUsuario, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "correoUsuario":
                            filtroBusqueda = builder.Eq(u => u.CorreoUsuario, valorFiltro);
                            break;
                        case "rolUsuario":
                            filtroBusqueda = builder.Eq(u => u.RolUsuario, valorFiltro);
                            break;
                        case "idSemillero":
                            if (int.TryParse(valorFiltro, out int idSem)) filtroBusqueda = builder.Eq(u => u.IdSemillero, idSem);
                            break;
                        case "nombreSemillero":
                            var semilleroEncontrado = listaSemilleros.FirstOrDefault(s => s.NombreSemillero.Equals(valorFiltro, StringComparison.OrdinalIgnoreCase));
                            filtroBusqueda = semilleroEncontrado != null ? builder.Eq(u => u.IdSemillero, semilleroEncontrado.IdSemillero) : builder.Eq(u => u.IdSemillero, -1);
                            break;
                    }
                }

                var filtroFinal = builder.And(filtroSeguridad, filtroBusqueda);
                List<DatosUsuario> listaFinal = coleccionUsuarios.Find(filtroFinal).ToList();

                return View(listaFinal);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al cargar la lista: " + ex.Message;
                return View(new List<DatosUsuario>());
            }
        }

        // GET: Usuarios/Agregar
        [HttpGet]
        public ActionResult Agregar()
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador")
            {
                TempData["Error"] = "No tienes permisos para agregar usuarios.";
                return RedirectToAction("Index");
            }

            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var ultimoUsuario = coleccionUsuarios.Find(new BsonDocument()).SortByDescending(u => u.IdUsuario).FirstOrDefault();

            var nuevoUsuario = new DatosUsuario();

            if (ultimoUsuario != null && ultimoUsuario.IdUsuario >= 200) nuevoUsuario.IdUsuario = ultimoUsuario.IdUsuario + 1;
            else nuevoUsuario.IdUsuario = 201;

            RecargarDatosVista(rolUsuario);
            return View(nuevoUsuario);
        }

        // POST: Usuarios/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosUsuario nuevoUsuario)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador") return RedirectToAction("Index");

            ModelState.Remove("IdUsuario");

            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var ultimoUsuario = coleccionUsuarios.Find(new BsonDocument()).SortByDescending(u => u.IdUsuario).FirstOrDefault();

            if (ultimoUsuario != null && ultimoUsuario.IdUsuario >= 200) nuevoUsuario.IdUsuario = ultimoUsuario.IdUsuario + 1;
            else nuevoUsuario.IdUsuario = 201;

            if (rolUsuario == "Líder") nuevoUsuario.IdSemillero = (int)Session["IdSemillero"];
            if (nuevoUsuario.RolUsuario == "Administrador" || nuevoUsuario.RolUsuario == "Admin") nuevoUsuario.IdSemillero = null;

            if (ModelState.IsValid)
            {
                // ==========================================
                // REGLA: El líder NO puede crear Administradores
                // ==========================================
                if (rolUsuario == "Líder" && (nuevoUsuario.RolUsuario == "Administrador" || nuevoUsuario.RolUsuario == "Admin"))
                {
                    TempData["Error"] = "Operación denegada: Un Líder no tiene permisos para crear usuarios con rol de Administrador.";
                    RecargarDatosVista(rolUsuario); return View(nuevoUsuario);
                }

                if (string.IsNullOrWhiteSpace(nuevoUsuario.NombreUsuario) || !Regex.IsMatch(nuevoUsuario.NombreUsuario, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                {
                    TempData["Error"] = "El nombre es obligatorio y solo puede contener letras y espacios.";
                    RecargarDatosVista(rolUsuario); return View(nuevoUsuario);
                }

                // ==========================================
                // REGLA: CELULAR COLOMBIANO (+57 -> 10 dígitos, empieza por 3)
                // ==========================================
                string telefonoStr = nuevoUsuario.TelefonoUsuario.HasValue ? nuevoUsuario.TelefonoUsuario.Value.ToString() : "";
                if (telefonoStr.Length != 10 || !telefonoStr.StartsWith("3"))
                {
                    TempData["Error"] = "El número debe tener exactamente 10 dígitos y empezar por el número 3 (Formato celular de Colombia).";
                    RecargarDatosVista(rolUsuario); return View(nuevoUsuario);
                }

                if (!nuevoUsuario.EdadUsuario.HasValue || nuevoUsuario.EdadUsuario.Value < 15 || nuevoUsuario.EdadUsuario.Value > 100)
                {
                    TempData["Error"] = "La edad es obligatoria y debe ser mayor a 15 años.";
                    RecargarDatosVista(rolUsuario); return View(nuevoUsuario);
                }

                if (string.IsNullOrWhiteSpace(nuevoUsuario.CorreoUsuario) || !Regex.IsMatch(nuevoUsuario.CorreoUsuario, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                {
                    TempData["Error"] = "El formato del correo electrónico no es válido.";
                    RecargarDatosVista(rolUsuario); return View(nuevoUsuario);
                }

                coleccionUsuarios.InsertOne(nuevoUsuario);
                TempData["Exito"] = $"Usuario registrado correctamente con el ID: {nuevoUsuario.IdUsuario}";
                return RedirectToAction("Index");
            }

            RecargarDatosVista(rolUsuario);
            return View(nuevoUsuario);
        }

        // GET: Usuarios/Modificar
        [HttpGet]
        public ActionResult Modificar(int id)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador")
            {
                TempData["Error"] = "No tienes permisos para modificar usuarios.";
                return RedirectToAction("Index");
            }

            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var usuario = coleccionUsuarios.Find(u => u.IdUsuario == id).FirstOrDefault();

            if (usuario == null) return RedirectToAction("Index");

            if (rolUsuario == "Líder" && usuario.IdSemillero != (int)Session["IdSemillero"])
            {
                TempData["Error"] = "Acceso denegado: Este usuario pertenece a otro semillero.";
                return RedirectToAction("Index");
            }

            RecargarDatosVista(rolUsuario);
            return View(usuario);
        }

        // POST: Usuarios/Modificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosUsuario usuarioModificado)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador") return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                var filtro = Builders<DatosUsuario>.Filter.Eq(u => u.IdUsuario, usuarioModificado.IdUsuario);

                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    usuarioModificado.IdSemillero = idSemilleroLider;
                    filtro = Builders<DatosUsuario>.Filter.And(
                        Builders<DatosUsuario>.Filter.Eq(u => u.IdUsuario, usuarioModificado.IdUsuario),
                        Builders<DatosUsuario>.Filter.Eq(u => u.IdSemillero, idSemilleroLider)
                    );
                }

                if (usuarioModificado.RolUsuario == "Administrador" || usuarioModificado.RolUsuario == "Admin")
                {
                    usuarioModificado.IdSemillero = null;
                }

                // ==========================================
                // REGLA: El líder NO puede escalar a un usuario a Administrador
                // ==========================================
                if (rolUsuario == "Líder" && (usuarioModificado.RolUsuario == "Administrador" || usuarioModificado.RolUsuario == "Admin"))
                {
                    TempData["Error"] = "Operación denegada: Un Líder no tiene permisos para ascender usuarios a Administrador.";
                    RecargarDatosVista(rolUsuario); return View(usuarioModificado);
                }

                if (string.IsNullOrWhiteSpace(usuarioModificado.NombreUsuario) || !Regex.IsMatch(usuarioModificado.NombreUsuario, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                {
                    TempData["Error"] = "El nombre es obligatorio y solo puede contener letras y espacios.";
                    RecargarDatosVista(rolUsuario); return View(usuarioModificado);
                }

                // ==========================================
                // REGLA: CELULAR COLOMBIANO
                // ==========================================
                string telefonoStr = usuarioModificado.TelefonoUsuario.HasValue ? usuarioModificado.TelefonoUsuario.Value.ToString() : "";
                if (telefonoStr.Length != 10 || !telefonoStr.StartsWith("3"))
                {
                    TempData["Error"] = "El número debe tener exactamente 10 dígitos y empezar por el número 3 (Formato celular de Colombia).";
                    RecargarDatosVista(rolUsuario); return View(usuarioModificado);
                }

                if (!usuarioModificado.EdadUsuario.HasValue || usuarioModificado.EdadUsuario.Value < 15 || usuarioModificado.EdadUsuario.Value > 100)
                {
                    TempData["Error"] = "La edad es obligatoria y debe ser mayor a 15 años.";
                    RecargarDatosVista(rolUsuario); return View(usuarioModificado);
                }

                if (string.IsNullOrWhiteSpace(usuarioModificado.CorreoUsuario) || !Regex.IsMatch(usuarioModificado.CorreoUsuario, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                {
                    TempData["Error"] = "El formato del correo electrónico no es válido.";
                    RecargarDatosVista(rolUsuario); return View(usuarioModificado);
                }

                var resultado = coleccionUsuarios.ReplaceOne(filtro, usuarioModificado);
                if (resultado.MatchedCount > 0) TempData["Exito"] = "La información se ha actualizado correctamente.";
                else TempData["Error"] = "No se pudo actualizar.";

                return RedirectToAction("Index");
            }

            RecargarDatosVista(rolUsuario);
            return View(usuarioModificado);
        }

        // GET: Usuarios/Eliminar
        public ActionResult Eliminar(int id)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador") return RedirectToAction("Index");

            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            DeleteResult resultado;

            if (rolUsuario == "Administrador" || rolUsuario == "Admin") resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id);
            else
            {
                int idSemilleroLider = (int)Session["IdSemillero"];
                resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id && u.IdSemillero == idSemilleroLider);
            }

            if (resultado.DeletedCount > 0) TempData["Exito"] = "El usuario ha sido eliminado permanentemente.";
            else TempData["Error"] = "No se pudo eliminar.";

            return RedirectToAction("Index");
        }

        private void RecargarDatosVista(string rolUsuario)
        {
            ViewBag.RolUsuario = rolUsuario;

            if (rolUsuario == "Administrador" || rolUsuario == "Admin")
            {
                var colSemilleros = conexionDB.Database.GetCollection<BsonDocument>("Semilleros");
                var lista = colSemilleros.Find(new BsonDocument()).ToList()
                            .Select(s => new { IdSemillero = s["idSemillero"].AsInt32, NombreSemillero = s["nombreSemillero"].AsString }).ToList();
                ViewBag.ListaSemilleros = new SelectList(lista, "IdSemillero", "NombreSemillero");
            }
            else if (rolUsuario == "Líder")
            {
                int idSemillero = (int)Session["IdSemillero"];
                ViewBag.IdSemilleroLider = idSemillero;
                var colSemilleros = conexionDB.Database.GetCollection<BsonDocument>("Semilleros");
                var semilleroDB = colSemilleros.Find(Builders<BsonDocument>.Filter.Eq("idSemillero", idSemillero)).FirstOrDefault();
                ViewBag.NombreSemilleroLider = (semilleroDB != null && semilleroDB.Contains("nombreSemillero")) ? semilleroDB["nombreSemillero"].AsString : "Desconocido";
            }
        }
    }
}