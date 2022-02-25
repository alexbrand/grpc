// Copyright 2015 gRPC authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Helloworld;
using Polly;

namespace GreeterClient
{
    class Program
    {
        public static void Main(string[] args)
        {
            Channel channel = new Channel("127.0.0.1:30051", ChannelCredentials.Insecure);
            var invoker = channel.Intercept(new SendThreeInterceptor());
            var client = new Greeter.GreeterClient(invoker);
            String user = "you";

      //var reply = Policy.Handle<RpcException>().WaitAndRetryAsync(3, (i) => TimeSpan.FromMilliseconds(10)).ExecuteAsync(async () => await client.SayHelloAsync(new HelloRequest { Name = user }));
            var reply = Task.Run(async () => await client.SayHelloAsync(new HelloRequest { Name = user }));
            Console.WriteLine("Greeting: " + reply.Result.Message);

            channel.ShutdownAsync().Wait();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

  class SendThreeInterceptor : Interceptor
  {
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
      return Polly1(request, context, continuation);
    }

    // Retries but need to call continuation twice
    private static AsyncUnaryCall<TResponse> PollyMaybe<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
      where TRequest : class
      where TResponse : class
    {
      var resp = continuation(request, context);
      var responseAsync = Policy.Handle<RpcException>().WaitAndRetryAsync(3, (i) => TimeSpan.FromMilliseconds(10)).ExecuteAsync(async () => await continuation(request, context));
      return new AsyncUnaryCall<TResponse>(responseAsync, resp.ResponseHeadersAsync, resp.GetStatus, resp.GetTrailers, resp.Dispose);
    }

    // Does not retry
    private static AsyncUnaryCall<TResponse> Polly1<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
      where TRequest : class
      where TResponse : class
    {
      return Policy.Handle<RpcException>().WaitAndRetry(3, (i) => TimeSpan.FromMilliseconds(10)).Execute(() => continuation(request, context));
    }

    // Handles the exception but blocks
    private static AsyncUnaryCall<TResponse> Attempt1<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
      where TRequest : class
      where TResponse : class
    {
      Exception lastEx = null;
      for (int i = 0; i < 3; i++)
      {
        try
        {
          var r = continuation(request, context);
          r.GetAwaiter().GetResult();
          return r;
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.ToString());
          lastEx = ex;
        }
      }
      throw lastEx;
    }

  }
}
