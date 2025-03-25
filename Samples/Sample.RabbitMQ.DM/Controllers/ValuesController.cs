using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Dm;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;


namespace Sample.RabbitMQ.DM.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly ICapPublisher _capBus;

        public ValuesController(ICapPublisher capPublisher)
        {
            _capBus = capPublisher;
        }

        [Route("~/without/transaction")]
        public async Task<IActionResult> WithoutTransaction()
        {
            await _capBus.PublishAsync("sample.rabbitmq.dm", DateTime.Now);

            return Ok();
        }

        [Route("~/adonet/transaction")]
        public IActionResult AdonetWithTransaction()
        {
            using (var connection = new DmConnection(Startup.ConnectionString))
            {
                using (var transaction = connection.BeginTransaction(_capBus, true))
                {
                    //your business code
                    connection.Execute("insert into test(name) values('test')", transaction: (IDbTransaction)transaction.DbTransaction);

                    //for (int i = 0; i < 5; i++)
                    //{
                    _capBus.Publish("sample.rabbitmq.dm", DateTime.Now);
                    //}
                }
            }

            return Ok();
        }



        [NonAction]
        [CapSubscribe("sample.rabbitmq.dm")]
        public void Subscriber(DateTime p)
        {
            Console.WriteLine($@"{DateTime.Now} Subscriber invoked, Info: {p}");
        }

        [NonAction]
        [CapSubscribe("sample.rabbitmq.dm", Group = "group.test2")]
        public void Subscriber2(DateTime p, [FromCap]CapHeader header)
        {
            Console.WriteLine($@"{DateTime.Now} Subscriber invoked, Info: {p}");
        }
    }
}
