﻿using System;
using System.Threading;
using UStateMachine;

namespace Program
{
    public static class Program
    {
        class Vec2
        {
            public float x, y;
        }

        class Move : UStateMachine<Vec2>.UState
        {
            protected internal override void Entry()
            {
                Console.WriteLine($"{Context.x} : {Context.y}");
                Console.WriteLine("Entry");
            }

            protected internal override void Update()
            {
                Context.y += 1;
                Console.WriteLine($"{Context.x} : {Context.y}");
                Console.WriteLine("Update");
            }

            protected internal override void Exit()
            {
                Console.WriteLine($"{Context.x} : {Context.y}");
                Console.WriteLine("Exit");
            }
        }

        public static void Main(string[] args)
        {
            var position = new Vec2();
            var stateMachine = new UStateMachine<Vec2>(position);
            stateMachine.RegisterState<Move>();
            stateMachine.SetStartState<Move>();

            int Count = 0;
            while (Count < 100)
            {
                stateMachine.Update();
                Count++;
                Thread.Sleep(50);
            }
        }
    }
}
