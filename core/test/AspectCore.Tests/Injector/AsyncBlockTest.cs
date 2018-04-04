using AspectCore.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace AspectCore.Tests.Injector
{
    public class Utility
    {
        public static void WriteLine(string value)
        {
            Debug.WriteLine(value);
        }
    }
    public class Intercept1 : AbstractInterceptorAttribute
    {
        public override Task Invoke(AspectContext context, AspectDelegate next)
        {
            context.Parameters[0] = "lemon";
            return context.Invoke(next);
        }
    }

    public class InvokeEndIntercept : AbstractInterceptorAttribute
    {
        public override Task Invoke(AspectContext context, AspectDelegate next)
        {
            var startTime = DateTime.Now;
            Utility.WriteLine($"{startTime}:start");
            var task = next(context);

            if (context.ReturnValue is Task resultTask)
            {
                resultTask.ContinueWith((o) =>
                {
                    // 被代理的方法已经执行
                    var startTimeInner = startTime;
                    var endTime = DateTime.Now;
                    Utility.WriteLine($"{endTime}:end");
                });
            }
            else
            {
                var endTime = DateTime.Now;
                Utility.WriteLine($"{endTime}:end");
            }
            return task;
        }
    }


    public class InvokeEndFailtIntercept : AbstractInterceptorAttribute
    {
        public async override Task Invoke(AspectContext context, AspectDelegate next)
        {
            var startTime = DateTime.Now;
            Utility.WriteLine($"{startTime}:start");
            //  var task= next(context);
            await next(context);
            //未等待被代理的方法执行
            var endTime = DateTime.Now;
            Utility.WriteLine($"{endTime}:end");
        }
    }


    public interface IService1
    {
        Task<string> GetValue(string val);

        Task<string> GetValue2(string val);

        Task<string> GetValue3(string val);

        Task GetValue4(string val);

    }

    public class Service1 : IService1
    {
        [Intercept1]
        public async Task<string> GetValue(string val)
        {
            await Task.Delay(3000);
            return val;
        }


        [InvokeEndIntercept]
        public async Task<string> GetValue2(string val)
        {
            await Task.Delay(4000);
            return val;
        }


        [InvokeEndFailtIntercept]
        public async Task<string> GetValue3(string val)
        {
            await Task.Delay(3000);

            await new Service1().GetValue4("1");
            Utility.WriteLine($"outer GetValue4-1");

            var builder = new ProxyGeneratorBuilder();
            builder.Configure(_ => { });
            var proxyGenerator = builder.Build();
            var proxy = proxyGenerator.CreateInterfaceProxy<IService1, Service1>();
            await proxy.GetValue4("2");
            Utility.WriteLine($"outer GetValue4-2");

            Utility.WriteLine($"inner GetValue3 1");
            return val;
        }

        [InvokeEndFailtIntercept]
        public async Task GetValue4(string val)
        {
            await Task.Delay(3000);
            Utility.WriteLine($"inner GetValue4-{val}");
        }
    }

    public class AsyncBlockTest : InjectorTestBase
    {


        [Fact]
        public async void AsyncBlock()
        {
            var builder = new ProxyGeneratorBuilder();
            builder.Configure(_ => { });
            var proxyGenerator = builder.Build();
            var proxy = proxyGenerator.CreateInterfaceProxy<IService1, Service1>();
            // IService proxy = new Service();
            var startTime = DateTime.Now;
            Utility.WriteLine($"{startTime}:start");

            var val = proxy.GetValue("le");

            var endTime = DateTime.Now;

            Assert.True((endTime - startTime).TotalSeconds < 2);
            Utility.WriteLine($"{endTime}:should return immediately");
            Utility.WriteLine($"{DateTime.Now}:{val.Result}");

            var val2 = await proxy.GetValue2("le2");
            Utility.WriteLine($"{val2}");


        }


        [Fact]
        public async void InvokeEndFailtIntercept()
        {
            var builder = new ProxyGeneratorBuilder();
            builder.Configure(_ => { });
            var proxyGenerator = builder.Build();
            var proxy = proxyGenerator.CreateInterfaceProxy<IService1, Service1>();
            // IService proxy = new Service();

            var val3 = await proxy.GetValue3("le3");
            Utility.WriteLine($"outer GetValue3-1");
        }
    }
}
