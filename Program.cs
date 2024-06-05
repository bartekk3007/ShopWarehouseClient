using System;
using MassTransit;
using MassTransit.Saga;
using Automatonymous;
using Komunikacja;

namespace Sklep
{
    public class RejestracjaZamowienie : SagaStateMachineInstance
    {
        public string Login { get; set; }
        public Guid CorrelationId { get; set; }
        public int Ilosc { get; set; }
        public Guid? TimeoutId { get; set; }
        public string CurrentState { get; set; }
    }
    public class RejestracjaSklep : MassTransitStateMachine<RejestracjaZamowienie>
    {
        public Event<StartZamowienia> StartZamowienia { get; private set; }
        public Schedule<RejestracjaZamowienie, Timeout> TO { get; set; }
        public Event<OdpowiedzWolne> OdpowiedzWolne { get; private set; }
        public Event<OdpowiedzWolneNegatywna> OdpowiedzWolneNegatywna { get; set; }
        public Event<Potwierdzenie> Potwierdzenie { get; private set; }
        public Event<Timeout> TimeoutEvent { get; private set; }
        public Event<BrakPotwierdzenia> BrakPotwierdzenia { get; private set; }
        public State PotwierdzoneKlient { get; private set; }
        public State PotwierdzoneMagazyn { get; private set; }
        public State Niepotwierdzone { get; private set; }

        public RejestracjaSklep()
        {
            InstanceState(x => x.CurrentState);

            Event(() => StartZamowienia,
                x => x.CorrelateBy(
                        s => s.Login,
                        ctx => ctx.Message.Login
                    ).SelectId(context => Guid.NewGuid())
                );

            Schedule(() => TO,
                    x => x.TimeoutId,
                    x => { x.Delay = TimeSpan.FromSeconds(10); }
                );

            Initially(
                When(StartZamowienia)
                .Schedule(TO, ctx => new Timeout() { CorrelationId = ctx.Instance.CorrelationId })
                .Then(ctx => ctx.Instance.Login = ctx.Data.Login)
                .Then(ctx => ctx.Instance.Ilosc = ctx.Data.Ilosc)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"Zamówienie {context.Data.Login} w ilości {context.Data.Ilosc}"); })
                .Respond(ctx => { return new PytanieoPotwierdzenie() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login }; })
                .Respond(ctx => { return new PytanieoWolne() { CorrelationId = ctx.Instance.CorrelationId, Ilosc = ctx.Instance.Ilosc }; })
                .TransitionTo(Niepotwierdzone)
                );

            During(PotwierdzoneMagazyn,
                When(TimeoutEvent)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"{context.Instance.Login} miał timeout na zamowienie o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new OdrzucenieZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize(),
                When(Potwierdzenie)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"{context.Instance.Login} potwierdził zamówienie o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new AkceptacjaZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Unschedule(TO)
                .Finalize(),
                When(BrakPotwierdzenia)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"{context.Instance.Login} nie potwierdził zamówienia o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new OdrzucenieZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize()
                );

            During(PotwierdzoneKlient,
                When(OdpowiedzWolne)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"Magazyn może zrealizować zamówienie o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new AkceptacjaZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize(),
                When(OdpowiedzWolneNegatywna)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"Magazyn nie może zrealizować zamówienia o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new OdrzucenieZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize()
                );

            During(Niepotwierdzone,
                When(TimeoutEvent)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"{context.Instance.Login} miał timeout na zamówienie o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new OdrzucenieZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize(),
                When(Potwierdzenie)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"{context.Instance.Login} potwierdził zamówienie o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Unschedule(TO)
                .TransitionTo(PotwierdzoneKlient),
                When(BrakPotwierdzenia)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"{context.Instance.Login} nie potwierdził zamówienia o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new OdrzucenieZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize(),
                When(OdpowiedzWolne)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"Magazyn może zrealizować zamówienie o identyfikatorze [{context.Data.CorrelationId}]"); })
                .TransitionTo(PotwierdzoneMagazyn),
                When(OdpowiedzWolneNegatywna)
                .ThenAsync(context => { return Console.Out.WriteLineAsync($"Magazyn nie może zrealizować zamówienia o identyfikatorze [{context.Data.CorrelationId}]"); })
                .Respond(ctx => { return new OdrzucenieZamowienia() { CorrelationId = ctx.Instance.CorrelationId, Login = ctx.Instance.Login, Ilosc = ctx.Instance.Ilosc }; })
                .Finalize()
                );

            SetCompletedWhenFinalized();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var repozytorium = new InMemorySagaRepository<RejestracjaZamowienie>();
            var saga = new RejestracjaSklep();
            var bus = Bus.Factory.CreateUsingRabbitMq(
            sbc =>
            {
                sbc.Host(
                    new Uri("rabbitmq://localhost"),
                    h => { h.Username("guest"); h.Password("guest"); });
                sbc.ReceiveEndpoint("saga",
                    ep => ep.StateMachineSaga(saga, repozytorium));
                sbc.UseInMemoryScheduler();
            });
            bus.Start();
            Console.WriteLine("Sklep");
            while (true) { }
        }
    }
}