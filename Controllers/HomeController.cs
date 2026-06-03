using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Bienvenida()
        {
            return View();
        }

        public ActionResult IniciarSesion()
        {
            return View();
        }
    }
}