using System;
using RATF.Tests;
using System.Linq;
class Runner {static int Main(){int failed=0;foreach(var test in RuleCases.All.Concat(ProviderCases.All)){try{test();Console.WriteLine("PASS "+test.Method.Name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+e.Message);}}return failed==0?0:1;}}
