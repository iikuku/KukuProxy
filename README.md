# KukuProxy
It's a simple C# socks5 proxy

# How to use
## 1. Copy server.cs and ProxyInfo.cs  to your project
## 2. writing some code to new and start the server like this:
```
Server proxy = new Server();
proxy.Port = 1234;
proxy.Start();
```

## If you want to watch the output info,  you can write a function and set  proxy.OutputFunc = your_funciton

# Why I upload these garbage code
When I find socks5 proxy source code with C# on  github,  I found nothing.
So I decide to build one.
After I read the protocol of socks5 I found it's too simple.
Finally I think my garbage code may help someone, so I decide to upload it.
Funny mud pee.