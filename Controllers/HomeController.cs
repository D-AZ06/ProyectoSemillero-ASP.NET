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
            try
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

                        // Solo asignamos el IdSemillero si NO es administrador (y si no es nulo)
                        // Ajusta la cadena "Administrador" al nombre exacto de tu rol
                        if (usuarioEncontrado.RolUsuario != "Administrador")
                        {
                            Session["IdSemillero"] = usuarioEncontrado.IdSemillero;
                        }
                        else
                        {
                            // Limpiamos la sesión o asignamos un valor por defecto si tu filtro lo exige
                            Session["IdSemillero"] = null;
                        }

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
            catch (Exception ex)
            {
                // Aquí puedes manejar cualquier excepción que ocurra durante el proceso de inicio de sesión
                ViewBag.ErrorLogin = "Ocurrió un error al intentar iniciar sesión. Por favor, inténtalo de nuevo.";
                return View(model);
            }
        }

        // Así debe quedar tu método para cerrar sesión
        public ActionResult CerrarSesion()
        {
            // 1. Destruye las variables de sesión
            Session.Clear();
            Session.Abandon();

            // 2. Mata la cookie de sesión del navegador
            if (Request.Cookies["ASP.NET_SessionId"] != null)
            {
                var cookie = new HttpCookie("ASP.NET_SessionId");
                cookie.Expires = DateTime.UtcNow.AddDays(-1);
                Response.Cookies.Add(cookie);
            }

            // 3. Redirige al login
            return RedirectToAction("IniciarSesion", "Home");
        }
    }
}