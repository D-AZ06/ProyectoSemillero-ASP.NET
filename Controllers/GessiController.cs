using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class GessiController : BaseController
    {
        // GET: Gessi
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }
    }
}