using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class UsuariosController : Controller
    {
        // GET: Usuarios
        private Conexion conexionDB = new Conexion();

        // GET: Usuarios
        public ActionResult Index()
        {
            try
            {
                // Seguridad por Rol
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                List<DatosUsuario> listaUsuarios = new List<DatosUsuario>();

                if (rolUsuario == "Administrador")
                {
                    listaUsuarios = coleccionUsuarios.Find(_ => true).ToList();
                }
                else
                {
                    if (Session["IdSemillero"] != null)
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        listaUsuarios = coleccionUsuarios.Find(u => u.IdSemillero == idSemillero).ToList();
                    }
                    else
                    {
                        TempData["Error"] = "Tu usuario no tiene un semillero asignado correctamente.";
                    }
                }

                return View(listaUsuarios);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al cargar la lista: " + ex.Message;
                return View(new List<DatosUsuario>()); // Retorna lista vacía en caso de error
            }
        }

        // GET: Usuarios/Agregar
        [HttpGet]
        public ActionResult Agregar()
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                // Restricción para Investigador
                if (Session["Rol"].ToString() == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para agregar usuarios.";
                    return RedirectToAction("Index");
                }

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al intentar abrir el formulario: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Usuarios/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosUsuario nuevoUsuario)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                // Restricción de seguridad extra por si fuerzan la petición POST
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para agregar usuarios.";
                    return RedirectToAction("Index");
                }

                ModelState.Remove("IdUsuario");

                if (ModelState.IsValid)
                {
                    var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

                    // 1. Lógica de ID automático
                    var ultimoUsuario = coleccionUsuarios.Find(new MongoDB.Bson.BsonDocument())
                                                         .SortByDescending(u => u.IdUsuario)
                                                         .FirstOrDefault();
                    int correlativo = 1;

                    if (ultimoUsuario != null)
                    {
                        string ultimoIdStr = ultimoUsuario.IdUsuario.ToString();
                        if (ultimoIdStr.StartsWith("20"))
                        {
                            string numeroStr = ultimoIdStr.Substring(2);
                            if (int.TryParse(numeroStr, out int numero))
                            {
                                correlativo = numero + 1;
                            }
                        }
                    }

                    nuevoUsuario.IdUsuario = int.Parse("20" + correlativo.ToString());

                    // 2. Asignación del Semillero
                    if (rolUsuario == "Líder")
                    {
                        nuevoUsuario.IdSemillero = (int)Session["IdSemillero"];
                    }
                    // NOTA: Si es Administrador, el IdSemillero se tomará de lo que 
                    // el Administrador haya llenado en el formulario HTML.

                    // 3. Guardar
                    coleccionUsuarios.InsertOne(nuevoUsuario);

                    TempData["Exito"] = $"El usuario ha sido registrado correctamente con el ID: {nuevoUsuario.IdUsuario}";
                    return RedirectToAction("Index");
                }

                return View(nuevoUsuario);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error inesperado al guardar el usuario: " + ex.Message;
                return View(nuevoUsuario);
            }
        }

        // GET: Usuarios/Modificar
        [HttpGet]
        public ActionResult Modificar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                // Restricción para Investigador
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar usuarios.";
                    return RedirectToAction("Index");
                }

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                var usuario = coleccionUsuarios.Find(u => u.IdUsuario == id).FirstOrDefault();

                if (usuario == null)
                {
                    TempData["Error"] = "No se encontró el usuario solicitado.";
                    return RedirectToAction("Index");
                }

                // Si es Líder, aseguramos que el usuario a editar pertenezca a su semillero
                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    if (usuario.IdSemillero != idSemilleroLider)
                    {
                        TempData["Error"] = "Acceso denegado: Este usuario pertenece a otro semillero.";
                        return RedirectToAction("Index");
                    }
                }

                return View(usuario);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al procesar la solicitud: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Usuarios/Modificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosUsuario usuarioModificado)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para modificar usuarios.";
                    return RedirectToAction("Index");
                }

                if (ModelState.IsValid)
                {
                    var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

                    // Construimos el filtro base
                    var filtro = Builders<DatosUsuario>.Filter.Eq(u => u.IdUsuario, usuarioModificado.IdUsuario);

                    // Si es Líder, forzamos que conserve su ID de Semillero y actualizamos solo los de su grupo
                    if (rolUsuario == "Líder")
                    {
                        int idSemilleroLider = (int)Session["IdSemillero"];
                        usuarioModificado.IdSemillero = idSemilleroLider;

                        // Filtro compuesto: Que coincida el ID de usuario Y el ID de Semillero
                        filtro = Builders<DatosUsuario>.Filter.And(
                            Builders<DatosUsuario>.Filter.Eq(u => u.IdUsuario, usuarioModificado.IdUsuario),
                            Builders<DatosUsuario>.Filter.Eq(u => u.IdSemillero, idSemilleroLider)
                        );
                    }

                    var resultado = coleccionUsuarios.ReplaceOne(filtro, usuarioModificado);

                    if (resultado.MatchedCount > 0)
                    {
                        TempData["Exito"] = "La información del usuario se ha actualizado correctamente.";
                    }
                    else
                    {
                        TempData["Error"] = "No se pudo actualizar. Es posible que no tengas permisos sobre este usuario.";
                    }

                    return RedirectToAction("Index");
                }

                return View(usuarioModificado);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error inesperado al modificar: " + ex.Message;
                return View(usuarioModificado);
            }
        }

        // GET: Usuarios/Eliminar
        public ActionResult Eliminar(int id)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();

                // Restricción para Investigador
                if (rolUsuario == "Investigador")
                {
                    TempData["Error"] = "No tienes permisos para eliminar usuarios.";
                    return RedirectToAction("Index");
                }

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");
                DeleteResult resultado;

                if (rolUsuario == "Administrador")
                {
                    // El administrador puede borrar cualquier usuario solo por su ID
                    resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id);
                }
                else
                {
                    // El líder solo puede borrar si coincide el ID del usuario Y su semillero
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id && u.IdSemillero == idSemilleroLider);
                }

                if (resultado.DeletedCount > 0)
                {
                    TempData["Exito"] = "El usuario ha sido eliminado permanentemente del sistema.";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar. Puede que el usuario ya no exista o pertenezca a otro semillero.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al intentar eliminar: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}