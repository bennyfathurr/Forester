using System;
class Runner {static int Main(){try{Forester.Tests.RuleCases.Run();Console.WriteLine("FORESTER_OFFLINE_PASS "+Forester.Tests.RuleCases.Passed);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}}
