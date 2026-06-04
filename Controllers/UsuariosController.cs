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
            // 1. SEGURIDAD: Verificamos que la sesión exista. 
            // Si alguien intenta entrar a la URL directo sin iniciar sesión, lo devolvemos al Login.
            if (Session["IdSemillero"] == null)
            {
                return RedirectToAction("IniciarSesion", "Home");
            }

            // 2. Extraemos el ID del semillero del líder que inició sesión
            int idSemilleroLider = (int)Session["IdSemillero"];

            // Apuntamos a la colección
            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

            // 3. EL FILTRO MAGICO: Le decimos a MongoDB que solo traiga los usuarios que 
            // tengan el mismo IdSemillero que el líder.
            List<DatosUsuario> listaUsuarios = coleccionUsuarios
                .Find(usuario => usuario.IdSemillero == idSemilleroLider)
                .ToList();

            // Enviamos la lista ya filtrada a la vista
            return View(listaUsuarios);
        }

        // GET: Usuarios/Agregar
        [HttpGet]
        public ActionResult Agregar()
        {
            // Seguridad: Verificamos que el líder esté logueado
            if (Session["IdSemillero"] == null)
            {
                return RedirectToAction("IniciarSesion", "Home");
            }

            return View();
        }

        // POST: Usuarios/Agregar
        // POST: Usuarios/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosUsuario nuevoUsuario)
        {
            if (Session["IdSemillero"] == null)
            {
                return RedirectToAction("IniciarSesion", "Home");
            }

            // Como el ID lo vamos a generar nosotros, le decimos al sistema 
            // que ignore si el formulario lo envió vacío, para que no dé error de validación.
            ModelState.Remove("IdUsuario");

            if (ModelState.IsValid)
            {
                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

                // --- LÓGICA DE ID AUTOMÁTICO (Ej: 208, 209, 2010) ---
                // 1. Buscamos el último usuario registrado ordenando por IdUsuario de mayor a menor
                var ultimoUsuario = coleccionUsuarios.Find(new MongoDB.Bson.BsonDocument())
                                                     .SortByDescending(u => u.IdUsuario)
                                                     .FirstOrDefault();

                int correlativo = 1; // Si no hay usuarios, empezamos en 1

                if (ultimoUsuario != null)
                {
                    string ultimoIdStr = ultimoUsuario.IdUsuario.ToString();

                    // Verificamos que empiece con "20" para no romper la lógica
                    if (ultimoIdStr.StartsWith("20"))
                    {
                        // Le quitamos los dos primeros caracteres ("20") para obtener el número real
                        string numeroStr = ultimoIdStr.Substring(2);
                        if (int.TryParse(numeroStr, out int numero))
                        {
                            correlativo = numero + 1; // Sumamos 1 al correlativo
                        }
                    }
                }

                // 2. Armamos el nuevo ID concatenando "20" + el correlativo
                string idGeneradoStr = "20" + correlativo.ToString();
                nuevoUsuario.IdUsuario = int.Parse(idGeneradoStr);
                // -----------------------------------------------------

                // 3. Asignamos el semillero del líder logueado
                nuevoUsuario.IdSemillero = (int)Session["IdSemillero"];

                // 4. Guardamos en MongoDB
                coleccionUsuarios.InsertOne(nuevoUsuario);

                // Pasamos el nuevo ID en el mensaje de éxito para que el usuario lo vea
                TempData["Exito"] = $"El usuario ha sido registrado correctamente con el ID: {nuevoUsuario.IdUsuario}";

                return RedirectToAction("Index");
            }

            return View(nuevoUsuario);
        }

        // GET: Usuarios/Modificar
        [HttpGet]
        public ActionResult Modificar(int id)
        {
            if (Session["IdSemillero"] == null)
            {
                return RedirectToAction("IniciarSesion", "Home");
            }

            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

            // Buscamos al usuario que coincida con el ID que envió el botón
            var usuario = coleccionUsuarios.Find(u => u.IdUsuario == id).FirstOrDefault();

            if (usuario == null)
            {
                TempData["Error"] = "No se encontró el usuario solicitado.";
                return RedirectToAction("Index");
            }

            // Le enviamos a la vista los datos encontrados para que llene el formulario
            return View(usuario);
        }

        // POST: Usuarios/Modificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosUsuario usuarioModificado)
        {
            if (Session["IdSemillero"] == null)
            {
                return RedirectToAction("IniciarSesion", "Home");
            }

            if (ModelState.IsValid)
            {
                // Por seguridad, aseguramos que mantenga el mismo ID de semillero del líder
                usuarioModificado.IdSemillero = (int)Session["IdSemillero"];

                var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

                // Creamos el filtro para decirle a Mongo a quién debe actualizar
                var filtro = Builders<DatosUsuario>.Filter.Eq(u => u.IdUsuario, usuarioModificado.IdUsuario);

                // Reemplazamos toda la información vieja por la nueva
                coleccionUsuarios.ReplaceOne(filtro, usuarioModificado);

                TempData["Exito"] = "La información del usuario se ha actualizado correctamente.";
                return RedirectToAction("Index");
            }

            // Si hay un error, devolvemos a la vista con los datos que intentaba guardar
            return View(usuarioModificado);
        }

        // GET: Usuarios/Eliminar
        public ActionResult Eliminar(int id)
        {
            // Seguridad de sesión
            if (Session["IdSemillero"] == null)
            {
                return RedirectToAction("IniciarSesion", "Home");
            }

            int idSemillero = (int)Session["IdSemillero"];
            var coleccionUsuarios = conexionDB.Database.GetCollection<DatosUsuario>("Usuarios");

            // Borramos el documento que coincida con el ID del usuario Y el ID del semillero
            var resultado = coleccionUsuarios.DeleteOne(u => u.IdUsuario == id && u.IdSemillero == idSemillero);

            if (resultado.DeletedCount > 0)
            {
                TempData["Exito"] = "El usuario ha sido eliminado permanentemente del sistema.";
            }
            else
            {
                TempData["Error"] = "No se pudo eliminar el usuario. Es posible que ya no exista o no tengas permisos.";
            }

            return RedirectToAction("Index");
        }
    }
}