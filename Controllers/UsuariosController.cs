using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

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

                // ==========================================
                // Obtener Nombres de los Semilleros para la vista
                // ==========================================
                var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                var listaSemilleros = coleccionSemilleros.Find(_ => true).ToList();
                ViewBag.DiccionarioSemilleros = listaSemilleros.ToDictionary(s => s.IdSemillero, s => s.nombreSemillero);
                // ==========================================

                var builder = Builders<DatosUsuario>.Filter;
                FilterDefinition<DatosUsuario> filtroSeguridad;

                // 1. FILTRO DE SEGURIDAD
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

                // 2. FILTRO DE BÚSQUEDA
                FilterDefinition<DatosUsuario> filtroBusqueda = builder.Empty;

                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    switch (tipoFiltro)
                    {
                        case "idUsuario":
                            if (int.TryParse(valorFiltro, out int idUsu))
                                filtroBusqueda = builder.Eq(u => u.IdUsuario, idUsu);
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
                            if (int.TryParse(valorFiltro, out int idSem))
                                filtroBusqueda = builder.Eq(u => u.IdSemillero, idSem);
                            break;
                        case "nombreSemillero":
                            var semilleroEncontrado = listaSemilleros.FirstOrDefault(s => s.nombreSemillero.Equals(valorFiltro, StringComparison.OrdinalIgnoreCase));
                            filtroBusqueda = semilleroEncontrado != null
                                ? builder.Eq(u => u.IdSemillero, semilleroEncontrado.IdSemillero)
                                : builder.Eq(u => u.IdSemillero, -1);
                            break;
                    }
                }

                // 3. COMBINAR Y EJECUTAR
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

            ViewBag.RolUsuario = rolUsuario;

            // ==============================================================
            // NUEVO: Calcular el próximo ID para mostrarlo en el formulario
            // ==============================================================
            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
            var ultimoUsuario = coleccionUsuarios.Find(new MongoDB.Bson.BsonDocument())
                                                 .SortByDescending(u => u.IdUsuario)
                                                 .FirstOrDefault();
            int correlativo = 1;

            if (ultimoUsuario != null)
            {
                string ultimoIdStr = ultimoUsuario.IdUsuario.ToString();
                if (ultimoIdStr.StartsWith("20"))
                {
                    if (int.TryParse(ultimoIdStr.Substring(2), out int numero)) correlativo = numero + 1;
                }
            }

            // Creamos un modelo vacío y le asignamos el ID calculado
            var nuevoUsuario = new DatosUsuario();
            nuevoUsuario.IdUsuario = int.Parse("20" + correlativo.ToString());
            // ==============================================================

            // Lógica para cargar Semilleros dependiendo del Rol
            if (rolUsuario == "Administrador" || rolUsuario == "Admin")
            {
                var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                var listaSemilleros = coleccionSemilleros.Find(new MongoDB.Bson.BsonDocument()).ToList().Select(s => new
                {
                    IdSemillero = s["idSemillero"].AsInt32,
                    NombreSemillero = s["nombreSemillero"].AsString
                }).ToList();

                ViewBag.ListaSemilleros = new SelectList(listaSemilleros, "IdSemillero", "NombreSemillero");
            }
            else if (rolUsuario == "Líder")
            {
                int idSemillero = (int)Session["IdSemillero"];
                ViewBag.IdSemilleroLider = idSemillero;

                var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                var filtro = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("idSemillero", idSemillero);
                var semilleroDB = coleccionSemilleros.Find(filtro).FirstOrDefault();

                ViewBag.NombreSemilleroLider = (semilleroDB != null && semilleroDB.Contains("nombreSemillero"))
                                                ? semilleroDB["nombreSemillero"].AsString
                                                : "Semillero Desconocido";
            }

            // IMPORTANTE: Ahora le enviamos el modelo 'nuevoUsuario' a la vista
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

            if (ModelState.IsValid)
            {
                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                var ultimoUsuario = coleccionUsuarios.Find(new BsonDocument()).SortByDescending(u => u.IdUsuario).FirstOrDefault();
                int correlativo = 1;

                if (ultimoUsuario != null)
                {
                    string ultimoIdStr = ultimoUsuario.IdUsuario.ToString();
                    if (ultimoIdStr.StartsWith("20"))
                    {
                        if (int.TryParse(ultimoIdStr.Substring(2), out int numero)) correlativo = numero + 1;
                    }
                }

                nuevoUsuario.IdUsuario = int.Parse("20" + correlativo.ToString());

                if (rolUsuario == "Líder")
                {
                    nuevoUsuario.IdSemillero = (int)Session["IdSemillero"];
                }

                // Si el rol que se está creando es Administrador, no debería tener semillero
                if (nuevoUsuario.RolUsuario == "Administrador" || nuevoUsuario.RolUsuario == "Admin")
                {
                    nuevoUsuario.IdSemillero = null;
                }

                coleccionUsuarios.InsertOne(nuevoUsuario);
                TempData["Exito"] = $"Usuario registrado correctamente con el ID: {nuevoUsuario.IdUsuario}";
                return RedirectToAction("Index");
            }
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

            ViewBag.RolUsuario = rolUsuario;

            // Misma lógica de semilleros para la vista de Modificar
            if (rolUsuario == "Administrador" || rolUsuario == "Admin")
            {
                var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                var listaSemilleros = coleccionSemilleros.Find(new MongoDB.Bson.BsonDocument()).ToList().Select(s => new
                {
                    IdSemillero = s["idSemillero"].AsInt32,
                    NombreSemillero = s["nombreSemillero"].AsString
                }).ToList();

                ViewBag.ListaSemilleros = new SelectList(listaSemilleros, "IdSemillero", "NombreSemillero");
            }
            else if (rolUsuario == "Líder")
            {
                int idSemillero = (int)Session["IdSemillero"];
                ViewBag.IdSemilleroLider = idSemillero;

                var coleccionSemilleros = conexionDB.Database.GetCollection<MongoDB.Bson.BsonDocument>("Semilleros");
                var filtro = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("idSemillero", idSemillero);
                var semilleroDB = coleccionSemilleros.Find(filtro).FirstOrDefault();

                ViewBag.NombreSemilleroLider = (semilleroDB != null && semilleroDB.Contains("nombreSemillero"))
                                                ? semilleroDB["nombreSemillero"].AsString
                                                : "Semillero Desconocido";
            }

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

                var resultado = coleccionUsuarios.ReplaceOne(filtro, usuarioModificado);
                if (resultado.MatchedCount > 0) TempData["Exito"] = "La información se ha actualizado correctamente.";
                else TempData["Error"] = "No se pudo actualizar.";

                return RedirectToAction("Index");
            }
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

            if (rolUsuario == "Administrador" || rolUsuario == "Admin")
            {
                resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id);
            }
            else
            {
                int idSemilleroLider = (int)Session["IdSemillero"];
                resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id && u.IdSemillero == idSemilleroLider);
            }

            if (resultado.DeletedCount > 0) TempData["Exito"] = "El usuario ha sido eliminado permanentemente.";
            else TempData["Error"] = "No se pudo eliminar.";

            return RedirectToAction("Index");
        }
    }
}