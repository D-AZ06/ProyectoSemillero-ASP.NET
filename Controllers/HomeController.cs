using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MongoDB.Driver;


namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public ActionResult Bienvenida()
        {
            return View();
        }

        [HttpGet]
        public ActionResult IniciarSesion()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult IniciarSesion(ValidarInicioSesion model)
        {
            if (ModelState.IsValid)
            {
                var conexion = new Conexion();
                var coleccion = conexion.Database.GetCollection<DatosUsuario>("Usuarios");

                var usuarioEncontrado = coleccion.Find(u =>
                    u.CorreoUsuario == model.Correo &&
                    u.ContrasenaUsuario == model.Contrasena).FirstOrDefault();

                if (usuarioEncontrado != null)
                {
                    // Guardamos las sesiones
                    Session["UsuarioLogueado"] = usuarioEncontrado.NombreUsuario;
                    Session["Rol"] = usuarioEncontrado.RolUsuario;
                    Session["IdUsuario"] = usuarioEncontrado.IdUsuario;

                    // ¡ESTA ES LA LÍNEA QUE FALTABA PARA QUE EL FILTRO FUNCIONE!
                    Session["IdSemillero"] = usuarioEncontrado.IdSemillero;

                    // EN LUGAR DE REDIRIGIR AQUÍ, LE AVISAMOS A LA VISTA QUE FUE EXITOSO
                    ViewBag.LoginExitoso = true;
                    return View(model);
                }
                else
                {
                    // LE AVISAMOS A LA VISTA QUE HUBO UN ERROR
                    ViewBag.ErrorLogin = "El correo o la contraseña son incorrectos.";
                    return View(model);
                }
            }

            return View(model);
        }

        // Así debe quedar tu método para cerrar sesión
        public ActionResult CerrarSesion()
        {
            // Destruye todas las variables
            Session.Clear();
            Session.Abandon();

            // Redirige silenciosamente a la vista de login
            return RedirectToAction("IniciarSesion", "Home");
        }
    }
}