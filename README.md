# Rebus distribuited Saga Exclusive Access Lock with Redis

This repository provides a Rebus implementation of saga exclusive access lock using Redis.

The default _EnforceExclusiveAccess_ extension already provides a _IExclusiveAccessLock_ parameter for custom implementation, but the issue with it is that _EnforceExclusiveSagaAccessIncomingStepBase_ is basing the lock key on a simple integer, that is not safe in environments where multiple instances of multiple applications are sharing the same redis connection.

A real scenario is when you have a microservice application using kubernetes, with many microservices and pods horizontally scaling, and maybe your saga storage is based on a NoSql like MongoDb.

With mongo you want to avoid concurrency exceptions during saga processing, so you enable the saga exclusive access lock.

With default EnforceExclusiveSagaAccessIncomingStepBase, having a simple "sagalock_{sagaId}" would be very unsafe because if you're sharing same redis connection from multiple microservices.

In this repo there is basically a COPY of the private class _EnforceExclusiveSagaAccessIncomingStepBase_ from Rebus original repository, BUT with a safe lock key based on saga full type name _$"{lockPrefix}:{a.SagaType}:{a.CorrelationPropertyName}:{a.CorrelationPropertyValue}"_
e.g. "prefix:namespace+sagaClassName:SagaId:123"

[![QA Build](https://github.com/pfab-io/RebusRedisExclusiveAccessLock/actions/workflows/dotnet.yml/badge.svg)](https://github.com/pfab-io/RebusRedisExclusiveAccessLock/actions/workflows/dotnet.yml)

## Setup
Install Install Nuget package [![NuGet](https://buildstats.info/nuget/PFabIO.Rebus.Sagas.Exclusive.Redis)](https://www.nuget.org/packages/PFabIO.Rebus.Sagas.Exclusive.Redis/ "Download PFabIO.Rebus.Sagas.Exclusive.Redis from NuGet.org")


   ```c#
   var connectionMultiplexer = ConnectionMultiplexer.Connect("...");
   
   serviceCollection.AddRebus((configurer, provider) 
      => configurer
         .Transport( .. )
         .Sagas(x =>
            {
                ...
                x.EnforceExclusiveAccessWithRedis(connectionMultiplexer: connectionMultiplexer);
            })
   });