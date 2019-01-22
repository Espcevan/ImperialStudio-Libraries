﻿using System;
using Castle.Windsor;
using UnityEngine;

namespace ImperialStudio.Core.Logging
{
    public class AnsiConsoleLogger : FormattedLogger
    {
        private const string ESC = "\u001b[";

        public AnsiConsoleLogger(IWindsorContainer container) : base(container)
        {
        }

        protected override void WriteColored(string format, Color? color = null, params object[] bindings)
        {
            if (color == null)
            {
                Console.Write(format, bindings);
                return;
            }

            Console.Write(ESC + $"38;2;{color.Value.r};{color.Value.g};{color.Value.b}m" + format, bindings);
            
            //reset color 
            Console.Write(ESC + "0m");
        }

        protected override void WriteLineColored(string format, Color? color = null, params object[] bindings)
        {
            if (color == null)
            {
                Console.WriteLine(format, bindings);
                return;
            }

            Console.WriteLine(ESC + $"38;2;{color.Value.r};{color.Value.g};{color.Value.b}m" + format, bindings);

            //reset color
            Console.Write(ESC + "0m");
        }

        public override string ServiceName => "AnsiConsole";
    }
}