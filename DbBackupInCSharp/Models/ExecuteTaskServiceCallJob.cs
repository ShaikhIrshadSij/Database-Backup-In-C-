using DbBackupInCSharp.Controllers;
using Quartz;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace DbBackupInCSharp.Models
{
    public class ExecuteTaskServiceCallJob : IJob
    {
        public static readonly string SchedulingStatus = ConfigurationManager.AppSettings["ExecuteTaskServiceCallSchedulingStatus"];
        public Task Execute(IJobExecutionContext context)
        {
            var task = Task.Run(() =>
            {
                if (SchedulingStatus.Equals("ON"))
                {
                    try
                    {
                        //HomeController _controller = new HomeController();
                        //_controller.GenerateBackupFile();
                        DriveServices.GenerateBackupFile();
                    }
                    catch (Exception ex)
                    {

                    }
                }

            });

            return task;
        }
    }
}