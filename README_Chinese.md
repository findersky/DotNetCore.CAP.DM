# DotNetCore.CAP.DM

## [English](README.md)

CAP 是一个基于 .Net Standard 的库，用于解决分布式事务问题，并具备事件总线（EventBus）的功能。它轻量级、易于使用且高效。

在构建 SOA 或微服务（MicroService）系统的过程中，我们通常需要使用事件来集成各个服务。然而，仅使用消息队列无法保证可靠性。CAP 采用与当前数据库集成的本地消息表方案，以解决分布式系统在相互调用过程中可能出现的异常。这可以确保事件消息在任何情况下都不会丢失。

此外，CAP 还可以用作事件总线（EventBus）。它提供了一种更简单的方式来实现事件的发布和订阅。在订阅和发送过程中，您无需继承或实现任何接口。

DotNetCore.CAP.DM 为 CAP 提供了对 DM（达梦数据库）的支持，使其能够与 DM 数据库集成。


## 安装
```
Install-Package DotNetCore.CAP.DM
```
## 使用
```csharp
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCap(x =>
            {
                x.UseStorageLock=true;
                x.UseDM(ConnectionString);
                x.UseRabbitMQ("localhost");
                x.UseDashboard();
            });
        }

```

```csharp
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCap(x =>
            {
                x.UseStorageLock=true;
                x.UseDM((option => {
                      option.ConnectionString = ConnectionString;
                      option.Schema = "SchemaName";
                 });
                x.UseRabbitMQ("localhost");
                x.UseDashboard();
            });
        }

```






