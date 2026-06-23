using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class GessiController : Controller
    {
        // GET: Gessi
        [HttpGet]
        public ActionResult Index()
        {
            // Verificamos la variable exacta que creaste en el Login
            if (Session["UsuarioLogueado"] == null)
            {
                // Si no hay sesión, al login de vuelta
                return RedirectToAction("IniciarSesion", "Home");
            }

            // Armamos el modelo con los datos de tu sesión para enviarlo a la vista
            DatosUsuario usuarioActual = new DatosUsuario
            {
                NombreUsuario = Session["UsuarioLogueado"].ToString(),
                RolUsuario = Session["Rol"].ToString()
            };

            return View(usuarioActual);
        }
    }
}