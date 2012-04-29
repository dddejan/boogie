//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Microsoft.Boogie {
  public class CommandLineOptionEngine
  {
    public readonly string ToolName;
    public readonly string DescriptiveToolName;
    [ContractInvariantMethod]
    void ObjectInvariant() {
      Contract.Invariant(ToolName != null);
      Contract.Invariant(DescriptiveToolName != null);
      Contract.Invariant(Environment != null);
      Contract.Invariant(cce.NonNullElements(Files));
    }

    public string/*!*/ Environment = "";
    public List<string/*!*/>/*!*/ Files = new List<string/*!*/>();
    public bool HelpRequested = false;
    public bool AttrHelpRequested = false;

    public CommandLineOptionEngine(string toolName, string descriptiveName) {
      Contract.Requires(toolName != null);
      Contract.Requires(descriptiveName != null);
      ToolName = toolName;
      DescriptiveToolName = descriptiveName;
    }

    public static string/*!*/ VersionNumber {
      get {
        Contract.Ensures(Contract.Result<string>() != null);
        return cce.NonNull(cce.NonNull(System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)).FileVersion);
      }
    }
    public static string/*!*/ VersionSuffix {
      get {
        Contract.Ensures(Contract.Result<string>() != null);
        return " version " + VersionNumber + ", Copyright (c) 2003-2011, Microsoft.";
      }
    }
    public string/*!*/ Version {
      get {
        Contract.Ensures(Contract.Result<string>() != null);
        return DescriptiveToolName + VersionSuffix;
      }
    }

    public string/*!*/ FileTimestamp = cce.NonNull(DateTime.Now.ToString("o")).Replace(':', '.');
    public void ExpandFilename(ref string pattern, string logPrefix, string fileTimestamp) {
      if (pattern != null) {
        pattern = pattern.Replace("@PREFIX@", logPrefix).Replace("@TIME@", fileTimestamp);
        string fn = Files.Count == 0 ? "" : Files[Files.Count - 1];
        fn = fn.Replace('/', '-').Replace('\\', '-');
        pattern = pattern.Replace("@FILE@", fn);
      }
    }

    /// <summary>
    /// Process the option and modify "ps" accordingly.
    /// Return true if the option is one that is recognized.
    /// </summary>
    protected virtual bool ParseOption(string name, CommandLineParseState ps) {
      Contract.Requires(name != null);
      Contract.Requires(ps != null);

      switch (name) {
        case "help":
        case "?":
          if (ps.ConfirmArgumentCount(0)) {
            HelpRequested = true;
          }
          return true;
        case "attrHelp":
          if (ps.ConfirmArgumentCount(0)) {
            AttrHelpRequested = true;
          }
          return true;
        default:
          break;
      }
      return false;  // unrecognized option
    }

    protected class CommandLineParseState
    {
      public string s;
      public bool hasColonArgument;
      public readonly string[]/*!*/ args;
      public int i;
      public int nextIndex;
      public bool EncounteredErrors;
      public readonly string ToolName;
      [ContractInvariantMethod]
      void ObjectInvariant() {
        Contract.Invariant(args != null);
        Contract.Invariant(0 <= i && i <= args.Length);
        Contract.Invariant(0 <= nextIndex && nextIndex <= args.Length);
      }


      public CommandLineParseState(string[] args, string toolName) {
        Contract.Requires(args != null);
        Contract.Requires(Contract.ForAll(0, args.Length, i => args[i] != null));
        Contract.Requires(toolName != null);
        Contract.Ensures(this.args == args);
        this.ToolName = toolName;
        this.s = null;  // set later by client
        this.hasColonArgument = false;  // set later by client
        this.args = args;
        this.i = 0;
        this.nextIndex = 0;  // set later by client
        this.EncounteredErrors = false;
      }

      public bool CheckBooleanFlag(string flagName, ref bool flag, bool valueWhenPresent) {
        Contract.Requires(flagName != null);
        //modifies nextIndex, encounteredErrors, Console.Error.*;
        bool flagPresent = false;

        if ((s == "/" + flagName || s == "-" + flagName) && ConfirmArgumentCount(0)) {
          flag = valueWhenPresent;
          flagPresent = true;
        }
        return flagPresent;
      }

      public bool CheckBooleanFlag(string flagName, ref bool flag) {
        Contract.Requires(flagName != null);
        //modifies nextIndex, encounteredErrors, Console.Error.*;
        return CheckBooleanFlag(flagName, ref flag, true);
      }

      /// <summary>
      /// If there is one argument and it is a non-negative integer, then set "arg" to that number and return "true".
      /// Otherwise, emit error message, leave "arg" unchanged, and return "false".
      /// </summary>
      public bool GetNumericArgument(ref int arg) {
        //modifies nextIndex, encounteredErrors, Console.Error.*;
        if (this.ConfirmArgumentCount(1)) {
          try {
            Contract.Assume(args[i] != null);
            Contract.Assert(args[i] is string);  // needed to prove args[i].IsPeerConsistent
            int d = Convert.ToInt32(this.args[this.i]);
            if (0 <= d) {
              arg = d;
              return true;
            }
          } catch (System.FormatException) {
          } catch (System.OverflowException) {
          }
        } else {
          return false;
        }
        Error("Invalid argument \"{0}\" to option {1}", args[this.i], this.s);
        return false;
      }

      /// <summary>
      /// If there is one argument and it is a non-negative integer less than "limit",
      /// then set "arg" to that number and return "true".
      /// Otherwise, emit error message, leave "arg" unchanged, and return "false".
      /// </summary>
      public bool GetNumericArgument(ref int arg, int limit) {
        Contract.Requires(this.i < args.Length);
        //modifies nextIndex, encounteredErrors, Console.Error.*;
        int a = arg;
        if (!GetNumericArgument(ref a)) {
          return false;
        } else if (a < limit) {
          arg = a;
          return true;
        } else {
          Error("Invalid argument \"{0}\" to option {1}", args[this.i], this.s);
          return false;
        }
      }

      /// <summary>
      /// If there is one argument and it is a non-negative real, then set "arg" to that number and return "true".
      /// Otherwise, emit an error message, leave "arg" unchanged, and return "false".
      /// </summary>
      public bool GetNumericArgument(ref double arg) {
        //modifies nextIndex, encounteredErrors, Console.Error.*;
        if (this.ConfirmArgumentCount(1)) {
          try {
            Contract.Assume(args[i] != null);
            Contract.Assert(args[i] is string);  // needed to prove args[i].IsPeerConsistent
            double d = Convert.ToDouble(this.args[this.i]);
            if (0 <= d) {
              arg = d;
              return true;
            }
          } catch (System.FormatException) {
          } catch (System.OverflowException) {
          }
        } else {
          return false;
        }
        Error("Invalid argument \"{0}\" to option {1}", args[this.i], this.s);
        return false;
      }

      public bool ConfirmArgumentCount(int argCount) {
        Contract.Requires(0 <= argCount);
        //modifies nextIndex, encounteredErrors, Console.Error.*;
        Contract.Ensures(Contract.Result<bool>() == (!(hasColonArgument && argCount != 1) && !(args.Length < i + argCount)));
        if (hasColonArgument && argCount != 1) {
          Error("\"{0}\" cannot take a colon argument", s);
          nextIndex = args.Length;
          return false;
        } else if (args.Length < i + argCount) {
          Error("\"{0}\" expects {1} argument{2}", s, argCount.ToString(), (string)(argCount == 1 ? "" : "s"));
          nextIndex = args.Length;
          return false;
        } else {
          nextIndex = i + argCount;
          return true;
        }
      }

      public void Error(string message, params string[] args) {
        Contract.Requires(args != null);
        Contract.Requires(message != null);
        //modifies encounteredErrors, Console.Error.*;
        Console.Error.WriteLine("{0}: Error: {1}", ToolName, String.Format(message, args));
        EncounteredErrors = true;
      }
    }

    public virtual void Usage() {
      Console.WriteLine("{0}: usage:  {0} [ option ... ] [ filename ... ]", ToolName);
      Console.WriteLine(@"  where <option> is one of

  ---- General options -------------------------------------------------------

  /help         this message
  /attrHelp     print a message about declaration attributes supported by
                this implementation");
    }

    public virtual void AttributeUsage() {
    }

    /// <summary>
    /// This method is called after all parsing is done, if no parse errors were encountered.
    /// </summary>
    protected virtual void ApplyDefaultOptions() {
    }
      
    /// <summary>
    /// Parses the command-line arguments "args" into the global flag variables.  Returns true
    /// if there were no errors.
    /// </summary>
    /// <param name="args">Consumed ("captured" and possibly modified) by the method.</param>
    public bool Parse([Captured] string[]/*!*/ args) {
      Contract.Requires(cce.NonNullElements(args));

      // save the command line options for the log files
      Environment += "Command Line Options: " + args.Concat(" ");
      args = cce.NonNull((string[])args.Clone());  // the operations performed may mutate the array, so make a copy
      var ps = new CommandLineParseState(args, ToolName);

      while (ps.i < args.Length) {
        cce.LoopInvariant(ps.args == args);
        ps.s = args[ps.i];
        Contract.Assert(ps.s != null);
        ps.s = ps.s.Trim();

        bool isOption = ps.s.StartsWith("-") || ps.s.StartsWith("/");
        int colonIndex = ps.s.IndexOf(':');
        if (0 <= colonIndex && isOption) {
          ps.hasColonArgument = true;
          args[ps.i] = ps.s.Substring(colonIndex + 1);
          ps.s = ps.s.Substring(0, colonIndex);
        } else {
          ps.i++;
          ps.hasColonArgument = false;
        }
        ps.nextIndex = ps.i;

        if (isOption) {
          if (!ParseOption(ps.s.Substring(1), ps)) {
            ps.Error("unknown switch: {0}", ps.s);
          }
        } else if (ps.ConfirmArgumentCount(0)) {
          string filename = ps.s;
          Files.Add(filename);
        }
        ps.i = ps.nextIndex;
      }

      if (HelpRequested) {
        Usage();
      } else if (AttrHelpRequested) {
        AttributeUsage();
      } else if (ps.EncounteredErrors) {
        Console.WriteLine("Use /help for available options");
      }

      if (ps.EncounteredErrors) {
        return false;
      } else {
        this.ApplyDefaultOptions();
        return true;
      }
    }

  }

  /// <summary>
  /// Boogie command-line options (other tools can subclass this class in order to support a
  /// superset of Boogie's options.
  /// </summary>
  public class CommandLineOptions : CommandLineOptionEngine {
    [ContractInvariantMethod]
    void ObjectInvariant() {
      Contract.Invariant(FileTimestamp != null);
    }

    public CommandLineOptions()
      : base("Boogie", "Boogie program verifier") {
    }

    protected CommandLineOptions(string toolName, string descriptiveName)
      : base(toolName, descriptiveName) {
      Contract.Requires(toolName != null);
      Contract.Requires(descriptiveName != null);
    }

    private static CommandLineOptions clo;
    public static CommandLineOptions/*!*/ Clo
    {
      get { return clo; }
    }

    public static void Install(CommandLineOptions options) {
      Contract.Requires(options != null);
      clo = options;
    }

    public const long Megabyte = 1048576;

    // Flags and arguments

    public bool RunningBoogieFromCommandLine = false;  // "false" means running Boogie from the plug-in

    [ContractInvariantMethod]
    void ObjectInvariant2() {
      Contract.Invariant(LogPrefix != null);
      Contract.Invariant(0 <= PrintUnstructured && PrintUnstructured < 3);  // 0 = print only structured,  1 = both structured and unstructured,  2 = only unstructured
    }

    public string PrintFile = null;
    public int PrintUnstructured = 0;
    public int DoomStrategy = -1;
    public bool DoomRestartTP = false;
    public bool PrintDesugarings = false;
    public string SimplifyLogFilePath = null;
    public string/*!*/ LogPrefix = "";
    public bool PrintInstrumented = false;
    public bool InstrumentWithAsserts = false;
    public enum InstrumentationPlaces {
      LoopHeaders,
      Everywhere
    }
    public InstrumentationPlaces InstrumentInfer = InstrumentationPlaces.LoopHeaders;
    public bool PrintWithUniqueASTIds = false;
    private string XmlSinkFilename = null;
    [Peer]
    public XmlSink XmlSink = null;
    public bool Wait = false;
    public bool Trace = false;
    public bool TraceTimes = false;
    public bool NoResolve = false;
    public bool NoTypecheck = false;
    public bool OverlookBoogieTypeErrors = false;
    public bool Verify = true;
    public bool TraceVerify = false;
    public int /*(0:3)*/ ErrorTrace = 1;
    public bool IntraproceduralInfer = true;
    public bool ContractInfer = false;
    public bool UseUnsatCoreForContractInfer = false;
    public bool PrintAssignment = false;
    public int InlineDepth = -1;
    public bool UseUncheckedContracts = false;
    public bool SimplifyLogFileAppend = false;
    public bool SoundnessSmokeTest = false;
    public string Z3ExecutablePath = null;

    public enum ProverWarnings {
      None,
      Stdout,
      Stderr
    }
    public ProverWarnings PrintProverWarnings = ProverWarnings.None;
    public int ProverShutdownLimit = 0;

    public enum SubsumptionOption {
      Never,
      NotForQuantifiers,
      Always
    }
    public SubsumptionOption UseSubsumption = SubsumptionOption.Always;

    public bool AlwaysAssumeFreeLoopInvariants = false;

    public enum ShowEnvironment {
      Never,
      DuringPrint,
      Always
    }
    public ShowEnvironment ShowEnv = ShowEnvironment.DuringPrint;
    public bool DontShowLogo = false;
    [ContractInvariantMethod]
    void ObjectInvariant3() {
      Contract.Invariant(-1 <= LoopFrameConditions && LoopFrameConditions < 3);
      Contract.Invariant(0 <= ModifiesDefault && ModifiesDefault < 7);
      Contract.Invariant((0 <= PrintErrorModel && PrintErrorModel <= 2) || PrintErrorModel == 4);
      Contract.Invariant(0 <= EnhancedErrorMessages && EnhancedErrorMessages < 2);
      Contract.Invariant(0 <= StepsBeforeWidening && StepsBeforeWidening <= 9);
      Contract.Invariant(-1 <= BracketIdsInVC && BracketIdsInVC < 2);
      Contract.Invariant(cce.NonNullElements(ProverOptions));
    }

    public int LoopUnrollCount = -1;  // -1 means don't unroll loops
    public int LoopFrameConditions = -1;  // -1 means not specified -- this will be replaced by the "implications" section below
    public int ModifiesDefault = 5;
    public bool LocalModifiesChecks = true;
    public bool NoVerifyByDefault = false;
    public enum OwnershipModelOption {
      Standard,
      Experimental,
      Trivial
    }
    public OwnershipModelOption OwnershipModelEncoding = OwnershipModelOption.Standard;
    public int PrintErrorModel = 0;
    public string PrintErrorModelFile = null;
    public string/*?*/ ModelViewFile = null;
    public int EnhancedErrorMessages = 0;
    public bool ForceBplErrors = false; // if true, boogie error is shown even if "msg" attribute is present
    public bool UseArrayTheory = false;
    public bool UseLabels = true;
    public bool MonomorphicArrays {
      get {
        return UseArrayTheory || TypeEncodingMethod == TypeEncoding.Monomorphic;
      }
    }
    public bool ExpandLambdas = true; // not useful from command line, only to be set to false programatically
    public bool DoModSetAnalysis = false;
    public bool DoBitVectorAnalysis = false;
    public string BitVectorAnalysisOutputBplFile = null;
    public bool UseAbstractInterpretation = true;          // true iff the user want to use abstract interpretation
    public int  /*0..9*/StepsBeforeWidening = 0;           // The number of steps that must be done before applying a widen operator


    public enum VCVariety {
      Structured,
      Block,
      Local,
      BlockNested,
      BlockReach,
      BlockNestedReach,
      Dag,
      DagIterative,
      Doomed,
      Unspecified
    }
    public VCVariety vcVariety = VCVariety.Unspecified;  // will not be Unspecified after command line has been parsed

    public bool RemoveEmptyBlocks = true;
    public bool CoalesceBlocks = true;

    [Rep]
    public ProverFactory TheProverFactory;
    public string ProverName;
    [Peer]
    public List<string/*!*/>/*!*/ ProverOptions = new List<string/*!*/>();

    public int BracketIdsInVC = -1;  // -1 - not specified, 0 - no, 1 - yes
    public bool CausalImplies = false;

    public int SimplifyProverMatchDepth = -1;  // -1 means not specified
    public int ProverKillTime = -1;  // -1 means not specified
    public int SmokeTimeout = 10; // default to 10s
    public int ProverCCLimit = 5;
    public bool z3AtFlag = true;
    public bool RestartProverPerVC = false;

    public double VcsMaxCost = 1.0;
    public double VcsPathJoinMult = 0.8;
    public double VcsPathCostMult = 1.0;
    public double VcsAssumeMult = 0.01;
    public double VcsPathSplitMult = 0.5; // 0.5-always, 2-rarely do path splitting
    public int VcsMaxSplits = 1;
    public int VcsMaxKeepGoingSplits = 1;
    public int VcsFinalAssertTimeout = 30;
    public int VcsKeepGoingTimeout = 1;
    public int VcsCores = 1;
    public bool VcsDumpSplits = false;

    public bool DebugRefuted = false;

    public XmlSink XmlRefuted {
      get {
        if (DebugRefuted)
          return XmlSink;
        else
          return null;
      }
    }
    [ContractInvariantMethod]
    void ObjectInvariant4() {
      Contract.Invariant(cce.NonNullElements(Z3Options));
      Contract.Invariant(0 <= Z3lets && Z3lets < 4);
    }

    [Peer]
    public List<string/*!*/>/*!*/ Z3Options = new List<string/*!*/>();
    public bool Z3types = false;
    public int Z3lets = 3;  // 0 - none, 1 - only LET TERM, 2 - only LET FORMULA, 3 - (default) any


    // Maximum amount of virtual memory (in bytes) for the prover to use
    //
    // Non-positive number indicates unbounded.
    public long MaxProverMemory = 100 * Megabyte;

    // Minimum number of prover calls before restart
    public int MinNumOfProverCalls = 5;

    public enum PlatformType {
      notSpecified,
      v1,
      v11,
      v2,
      cli1
    }
    public PlatformType TargetPlatform;
    public string TargetPlatformLocation;
    public string StandardLibraryLocation;

    // whether procedure inlining is enabled at call sites.
    public enum Inlining {
      None,
      Assert,
      Assume,
      Spec
    };
    public Inlining ProcedureInlining = Inlining.Assume;
    public bool PrintInlined = false;
    public bool ExtractLoops = false;
    public int StratifiedInlining = 0;
    public int StratifiedInliningOption = 0;
    public bool StratifiedInliningWithoutModels = false; // disable model generation for SI
    public int StratifiedInliningVerbose = 0; // verbosity level
    public bool BctModeForStratifiedInlining = false;
    public int RecursionBound = 500;
    public bool NonUniformUnfolding = false;
    public string inferLeastForUnsat = null;
    public string CoverageReporterPath = null;
    public Process coverageReporter = null; // used internally for debugging

    public enum TypeEncoding {
      None,
      Predicates,
      Arguments,
      Monomorphic
    };
    public TypeEncoding TypeEncodingMethod = TypeEncoding.Predicates;

    public bool Monomorphize = false;

    public bool ReflectAdd = false;

    public int LiveVariableAnalysis = 1;

    // Static constructor
    static CommandLineOptions() {
      if (System.Type.GetType("Mono.Runtime") == null) {  // MONO
        TraceListenerCollection/*!*/ dbl = Debug.Listeners;
        Contract.Assert(dbl != null);
        Contract.Assume(cce.IsPeerConsistent(dbl));  // hangs off static field
        dbl.Add(new DefaultTraceListener());
      }
    }

    public List<string/*!*/> ProcsToCheck = null;  // null means "no restriction"
    [ContractInvariantMethod]
    void ObjectInvariant5() {
      Contract.Invariant(cce.NonNullElements(ProcsToCheck, true));
      Contract.Invariant(Ai != null);
    }

    public class AiFlags {
      public bool Intervals = false;
      public bool Constant = false;
      public bool DynamicType = false;
      public bool Nullness = false;
      public bool Polyhedra = false;
      public bool J_Trivial = false;
      public bool J_Intervals = false;
      public bool DebugStatistics = false;

      public bool AnySet {
        get {
          return Intervals
              || Constant
              || DynamicType
              || Nullness
              || Polyhedra
              || J_Trivial
              || J_Intervals;
        }
      }
    }
    public AiFlags/*!*/ Ai = new AiFlags();

    protected override bool ParseOption(string name, CommandLineOptionEngine.CommandLineParseState ps) {
      var args = ps.args;  // convenient synonym
      switch (name) {
        case "infer":
          if (ps.ConfirmArgumentCount(1)) {
            foreach (char c in cce.NonNull(args[ps.i])) {
              switch (c) {
                case 'i':
                  Ai.Intervals = true;
                  UseAbstractInterpretation = true;
                  break;
                case 'c':
                  Ai.Constant = true;
                  UseAbstractInterpretation = true;
                  break;
                case 'd':
                  Ai.DynamicType = true;
                  UseAbstractInterpretation = true;
                  break;
                case 'n':
                  Ai.Nullness = true;
                  UseAbstractInterpretation = true;
                  break;
                case 'p':
                  Ai.Polyhedra = true;
                  UseAbstractInterpretation = true;
                  break;
                case 't':
                  Ai.J_Trivial = true;
                  UseAbstractInterpretation = true;
                  break;
                case 'j':
                  Ai.J_Intervals = true;
                  UseAbstractInterpretation = true;
                  break;
                case 's':
                  Ai.DebugStatistics = true;
                  UseAbstractInterpretation = true;
                  break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                  StepsBeforeWidening = (int)char.GetNumericValue(c);
                  break;
                default:
                  ps.Error("Invalid argument '{0}' to option {1}", c.ToString(), ps.s);
                  break;
              }
            }
          }
          return true;

        case "noinfer":
          if (ps.ConfirmArgumentCount(0)) {
            UseAbstractInterpretation = false;
          }
          return true;

        case "logInfer":
          if (ps.ConfirmArgumentCount(0)) {
            Microsoft.AbstractInterpretationFramework.Lattice.LogSwitch = true;
          }
          return true;

        case "break":
        case "launch":
          if (ps.ConfirmArgumentCount(0)) {
            System.Diagnostics.Debugger.Launch();
          }
          return true;

        case "proc":
          if (ProcsToCheck == null) {
            ProcsToCheck = new List<string/*!*/>();
          }
          if (ps.ConfirmArgumentCount(1)) {
            ProcsToCheck.Add(cce.NonNull(args[ps.i]));
          }
          return true;

        case "xml":
          if (ps.ConfirmArgumentCount(1)) {
            XmlSinkFilename = args[ps.i];
          }
          return true;

        case "print":
          if (ps.ConfirmArgumentCount(1)) {
            PrintFile = args[ps.i];
          }
          return true;

        case "proverLog":
          if (ps.ConfirmArgumentCount(1)) {
            SimplifyLogFilePath = args[ps.i];
          }
          return true;

        case "logPrefix":
          if (ps.ConfirmArgumentCount(1)) {
            string s = cce.NonNull(args[ps.i]);
            LogPrefix += s.Replace('/', '-').Replace('\\', '-');
          }
          return true;

        case "proverShutdownLimit":
          ps.GetNumericArgument(ref ProverShutdownLimit);
          return true;

        case "errorTrace":
          ps.GetNumericArgument(ref ErrorTrace, 3);
          return true;

        case "proverWarnings": {
            int pw = 0;
            if (ps.GetNumericArgument(ref pw, 3)) {
              switch (pw) {
                case 0:
                  PrintProverWarnings = ProverWarnings.None;
                  break;
                case 1:
                  PrintProverWarnings = ProverWarnings.Stdout;
                  break;
                case 2:
                  PrintProverWarnings = ProverWarnings.Stderr;
                  break;
                default: {
                    Contract.Assert(false);
                    throw new cce.UnreachableException();
                  } // postcondition of GetNumericArgument guarantees that we don't get here
              }
            }
            return true;
          }

        case "env": {
            int e = 0;
            if (ps.GetNumericArgument(ref e, 3)) {
              switch (e) {
                case 0:
                  ShowEnv = ShowEnvironment.Never;
                  break;
                case 1:
                  ShowEnv = ShowEnvironment.DuringPrint;
                  break;
                case 2:
                  ShowEnv = ShowEnvironment.Always;
                  break;
                default: {
                    Contract.Assert(false);
                    throw new cce.UnreachableException();
                  } // postcondition of GetNumericArgument guarantees that we don't get here
              }
            }
            return true;
          }

        case "loopUnroll":
          ps.GetNumericArgument(ref LoopUnrollCount);
          return true;

        case "printModel":
          if (ps.ConfirmArgumentCount(1)) {
            switch (args[ps.i]) {
              case "0":
                PrintErrorModel = 0;
                break;
              case "1":
                PrintErrorModel = 1;
                break;
              case "2":
                PrintErrorModel = 2;
                break;
              case "4":
                PrintErrorModel = 4;
                break;
              default:
                ps.Error("Invalid argument \"{0}\" to option {1}", args[ps.i], ps.s);
                break;
            }
          }
          return true;

        case "mv":
          if (ps.ConfirmArgumentCount(1)) {
            ModelViewFile = args[ps.i];
          }
          return true;

        case "printModelToFile":
          if (ps.ConfirmArgumentCount(1)) {
            PrintErrorModelFile = args[ps.i];
          }
          return true;

        case "enhancedErrorMessages":
          ps.GetNumericArgument(ref EnhancedErrorMessages, 2);
          return true;

        case "inlineDepth":
          ps.GetNumericArgument(ref InlineDepth);
          return true;

        case "subsumption": {
            int s = 0;
            if (ps.GetNumericArgument(ref s, 3)) {
              switch (s) {
                case 0:
                  UseSubsumption = SubsumptionOption.Never;
                  break;
                case 1:
                  UseSubsumption = SubsumptionOption.NotForQuantifiers;
                  break;
                case 2:
                  UseSubsumption = SubsumptionOption.Always;
                  break;
                default: {
                    Contract.Assert(false);
                    throw new cce.UnreachableException();
                  } // postcondition of GetNumericArgument guarantees that we don't get here
              }
            }
            return true;
          }

        case "liveVariableAnalysis": {
            int lva = 0;
            if (ps.GetNumericArgument(ref lva, 3)) {
              LiveVariableAnalysis = lva;
            }
            return true;
          }

        case "removeEmptyBlocks": {
            int reb = 0;
            if (ps.GetNumericArgument(ref reb, 2)) {
              RemoveEmptyBlocks = reb == 1;
            }
            return true;
          }

        case "coalesceBlocks": {
            int cb = 0;
            if (ps.GetNumericArgument(ref cb, 2)) {
              CoalesceBlocks = cb == 1;
            }
            return true;
          }

        case "vc":
          if (ps.ConfirmArgumentCount(1)) {
            switch (args[ps.i]) {
              case "s":
              case "structured":
                vcVariety = VCVariety.Structured;
                break;
              case "b":
              case "block":
                vcVariety = VCVariety.Block;
                break;
              case "l":
              case "local":
                vcVariety = VCVariety.Local;
                break;
              case "n":
              case "nested":
                vcVariety = VCVariety.BlockNested;
                break;
              case "m":
                vcVariety = VCVariety.BlockNestedReach;
                break;
              case "r":
                vcVariety = VCVariety.BlockReach;
                break;
              case "d":
              case "dag":
                vcVariety = VCVariety.Dag;
                break;
              case "i":
                vcVariety = VCVariety.DagIterative;
                break;
              case "doomed":
                vcVariety = VCVariety.Doomed;
                break;
              default:
                ps.Error("Invalid argument \"{0}\" to option {1}", args[ps.i], ps.s);
                break;
            }
          }
          return true;

        case "prover":
          if (ps.ConfirmArgumentCount(1)) {
            TheProverFactory = ProverFactory.Load(cce.NonNull(args[ps.i]));
            ProverName = cce.NonNull(args[ps.i]).ToUpper();
          }
          return true;

        case "p":
        case "proverOpt":
          if (ps.ConfirmArgumentCount(1)) {
            ProverOptions.Add(cce.NonNull(args[ps.i]));
          }
          return true;

        case "DoomStrategy":
          ps.GetNumericArgument(ref DoomStrategy);
          return true;

        case "DoomRestartTP":
          if (ps.ConfirmArgumentCount(0)) {
            DoomRestartTP = true;
          }
          return true;

        case "extractLoops":
          if (ps.ConfirmArgumentCount(0)) {
            ExtractLoops = true;
          }
          return true;

        case "inline":
          if (ps.ConfirmArgumentCount(1)) {
            switch (args[ps.i]) {
              case "none":
                ProcedureInlining = Inlining.None;
                break;
              case "assert":
                ProcedureInlining = Inlining.Assert;
                break;
              case "assume":
                ProcedureInlining = Inlining.Assume;
                break;
              case "spec":
                ProcedureInlining = Inlining.Spec;
                break;
              default:
                ps.Error("Invalid argument \"{0}\" to option {1}", args[ps.i], ps.s);
                break;
            }
          }
          return true;

        case "stratifiedInline":
          if (ps.ConfirmArgumentCount(1)) {
            switch (args[ps.i]) {
              case "0":
                StratifiedInlining = 0;
                break;
              case "1":
                StratifiedInlining = 1;
                break;
              default:
                StratifiedInlining = Int32.Parse(cce.NonNull(args[ps.i]));
                //ps.Error("Invalid argument \"{0}\" to option {1}", args[ps.i], ps.s);
                break;
            }
          }
          return true;
        case "siVerbose":
          if (ps.ConfirmArgumentCount(1)) {
            StratifiedInliningVerbose = Int32.Parse(cce.NonNull(args[ps.i]));
          }
          return true;
        case "siBct":
          if (ps.ConfirmArgumentCount(1))
          {
              BctModeForStratifiedInlining = (Int32.Parse(cce.NonNull(args[ps.i])) == 1);
          }
          return true;
        case "recursionBound":
          if (ps.ConfirmArgumentCount(1)) {
            RecursionBound = Int32.Parse(cce.NonNull(args[ps.i]));
          }
          return true;

        case "coverageReporter":
          if (ps.ConfirmArgumentCount(1)) {
            CoverageReporterPath = args[ps.i];
          }
          return true;

        case "stratifiedInlineOption":
          if (ps.ConfirmArgumentCount(1)) {
            StratifiedInliningOption = Int32.Parse(cce.NonNull(args[ps.i]));
          }
          return true;

        case "inferLeastForUnsat":
          if (ps.ConfirmArgumentCount(1)) {
            inferLeastForUnsat = args[ps.i];
          }
          return true;

        case "typeEncoding":
          if (ps.ConfirmArgumentCount(1)) {
            switch (args[ps.i]) {
              case "n":
              case "none":
                TypeEncodingMethod = TypeEncoding.None;
                break;
              case "p":
              case "predicates":
                TypeEncodingMethod = TypeEncoding.Predicates;
                break;
              case "a":
              case "arguments":
                TypeEncodingMethod = TypeEncoding.Arguments;
                break;
              case "m":
              case "monomorphic":
                TypeEncodingMethod = TypeEncoding.Monomorphic;
                break;
              default:
                ps.Error("Invalid argument \"{0}\" to option {1}", args[ps.i], ps.s);
                break;
            }
          }
          return true;

        case "instrumentInfer":
          if (ps.ConfirmArgumentCount(1)) {
            switch (args[ps.i]) {
              case "e":
                InstrumentInfer = InstrumentationPlaces.Everywhere;
                break;
              case "h":
                InstrumentInfer = InstrumentationPlaces.LoopHeaders;
                break;
              default:
                ps.Error("Invalid argument \"{0}\" to option {1}", args[ps.i], ps.s);
                break;
            }
          }
          return true;

        case "vcBrackets":
          ps.GetNumericArgument(ref BracketIdsInVC, 2);
          return true;

        case "proverMemoryLimit": {
            int d = 0;
            if (ps.GetNumericArgument(ref d)) {
              MaxProverMemory = d * Megabyte;
            }
            return true;
          }

        case "vcsMaxCost":
          ps.GetNumericArgument(ref VcsMaxCost);
          return true;

        case "vcsPathJoinMult":
          ps.GetNumericArgument(ref VcsPathJoinMult);
          return true;

        case "vcsPathCostMult":
          ps.GetNumericArgument(ref VcsPathCostMult);
          return true;

        case "vcsAssumeMult":
          ps.GetNumericArgument(ref VcsAssumeMult);
          return true;

        case "vcsPathSplitMult":
          ps.GetNumericArgument(ref VcsPathSplitMult);
          return true;

        case "vcsMaxSplits":
          ps.GetNumericArgument(ref VcsMaxSplits);
          return true;

        case "vcsMaxKeepGoingSplits":
          ps.GetNumericArgument(ref VcsMaxKeepGoingSplits);
          return true;

        case "vcsFinalAssertTimeout":
          ps.GetNumericArgument(ref VcsFinalAssertTimeout);
          return true;

        case "vcsKeepGoingTimeout":
          ps.GetNumericArgument(ref VcsKeepGoingTimeout);
          return true;

        case "vcsCores":
          ps.GetNumericArgument(ref VcsCores);
          return true;

        case "simplifyMatchDepth":
          ps.GetNumericArgument(ref SimplifyProverMatchDepth);
          return true;

        case "timeLimit":
          ps.GetNumericArgument(ref ProverKillTime);
          return true;

        case "smokeTimeout":
          ps.GetNumericArgument(ref SmokeTimeout);
          return true;

        case "errorLimit":
          ps.GetNumericArgument(ref ProverCCLimit);
          return true;

        case "z3opt":
          if (ps.ConfirmArgumentCount(1)) {
            Z3Options.Add(cce.NonNull(args[ps.i]));
          }
          return true;

        case "z3lets":
          ps.GetNumericArgument(ref Z3lets, 4);
          return true;

        case "platform":
          if (ps.ConfirmArgumentCount(1)) {
            StringCollection platformOptions = this.ParseNamedArgumentList(args[ps.i]);
            if (platformOptions != null && platformOptions.Count > 0) {
              try {
                this.TargetPlatform = (PlatformType)cce.NonNull(Enum.Parse(typeof(PlatformType), cce.NonNull(platformOptions[0])));
              } catch {
                ps.Error("Bad /platform type '{0}'", platformOptions[0]);
                break;
              }
              if (platformOptions.Count > 1) {
                this.TargetPlatformLocation = platformOptions[1];
                if (!Directory.Exists(platformOptions[1])) {
                  ps.Error("/platform directory '{0}' does not exist", platformOptions[1]);
                  break;
                }
              }
            }
          }
          return true;

        case "z3exe":
          if (ps.ConfirmArgumentCount(1)) {
            Z3ExecutablePath = args[ps.i];
          }
          return true;

        case "doBitVectorAnalysis":
          DoBitVectorAnalysis = true;
          if (ps.ConfirmArgumentCount(1)) {
            BitVectorAnalysisOutputBplFile = args[ps.i];
          }
          return true;

        default:
          bool optionValue = false;
          if (ps.CheckBooleanFlag("printUnstructured", ref optionValue)) {
            PrintUnstructured = optionValue ? 1 : 0;
            return true;
          }

          if (ps.CheckBooleanFlag("printDesugared", ref PrintDesugarings) ||
              ps.CheckBooleanFlag("printInstrumented", ref PrintInstrumented) ||
              ps.CheckBooleanFlag("printWithUniqueIds", ref PrintWithUniqueASTIds) ||
              ps.CheckBooleanFlag("wait", ref Wait) ||
              ps.CheckBooleanFlag("trace", ref Trace) ||
              ps.CheckBooleanFlag("traceTimes", ref TraceTimes) ||
              ps.CheckBooleanFlag("noResolve", ref NoResolve) ||
              ps.CheckBooleanFlag("noTypecheck", ref NoTypecheck) ||
              ps.CheckBooleanFlag("overlookTypeErrors", ref OverlookBoogieTypeErrors) ||
              ps.CheckBooleanFlag("noVerify", ref Verify, false) ||
              ps.CheckBooleanFlag("traceverify", ref TraceVerify) ||
              ps.CheckBooleanFlag("alwaysAssumeFreeLoopInvariants", ref AlwaysAssumeFreeLoopInvariants, true) ||
              ps.CheckBooleanFlag("nologo", ref DontShowLogo) ||
              ps.CheckBooleanFlag("proverLogAppend", ref SimplifyLogFileAppend) ||
              ps.CheckBooleanFlag("checkInfer", ref InstrumentWithAsserts) ||
              ps.CheckBooleanFlag("interprocInfer", ref IntraproceduralInfer, false) ||
              ps.CheckBooleanFlag("restartProver", ref RestartProverPerVC) ||
              ps.CheckBooleanFlag("printInlined", ref PrintInlined) ||
              ps.CheckBooleanFlag("smoke", ref SoundnessSmokeTest) ||
              ps.CheckBooleanFlag("vcsDumpSplits", ref VcsDumpSplits) ||
              ps.CheckBooleanFlag("dbgRefuted", ref DebugRefuted) ||
              ps.CheckBooleanFlag("causalImplies", ref CausalImplies) ||
              ps.CheckBooleanFlag("reflectAdd", ref ReflectAdd) ||
              ps.CheckBooleanFlag("z3types", ref Z3types) ||
              ps.CheckBooleanFlag("z3multipleErrors", ref z3AtFlag, false) ||
              ps.CheckBooleanFlag("monomorphize", ref Monomorphize) ||
              ps.CheckBooleanFlag("useArrayTheory", ref UseArrayTheory) ||
              ps.CheckBooleanFlag("doModSetAnalysis", ref DoModSetAnalysis) ||
              ps.CheckBooleanFlag("doNotUseLabels", ref UseLabels, false) ||
              ps.CheckBooleanFlag("contractInfer", ref ContractInfer) ||
              ps.CheckBooleanFlag("useUnsatCoreForContractInfer", ref UseUnsatCoreForContractInfer) ||
              ps.CheckBooleanFlag("printAssignment", ref PrintAssignment) ||
              ps.CheckBooleanFlag("nonUniformUnfolding", ref NonUniformUnfolding)
              ) {
            // one of the boolean flags matched
            return true;
          }
          break;
      }

      return base.ParseOption(name, ps);  // defer to superclass
    }

    protected override void ApplyDefaultOptions() {
      Contract.Ensures(TheProverFactory != null);
      Contract.Ensures(vcVariety != VCVariety.Unspecified);

      base.ApplyDefaultOptions();

      // expand macros in filenames, now that LogPrefix is fully determined
      ExpandFilename(ref XmlSinkFilename, LogPrefix, FileTimestamp);
      ExpandFilename(ref PrintFile, LogPrefix, FileTimestamp);
      ExpandFilename(ref SimplifyLogFilePath, LogPrefix, FileTimestamp);
      ExpandFilename(ref PrintErrorModelFile, LogPrefix, FileTimestamp);

      Contract.Assume(XmlSink == null);  // XmlSink is to be set here
      if (XmlSinkFilename != null) {
        XmlSink = new XmlSink(XmlSinkFilename);
      }

      if (TheProverFactory == null) {
        TheProverFactory = ProverFactory.Load("SMTLIB");
        ProverName = "SMTLIB".ToUpper();
      }

      if (vcVariety == VCVariety.Unspecified) {
        vcVariety = TheProverFactory.DefaultVCVariety;
      }

      if (UseArrayTheory) {
        Monomorphize = true;
      }

      if (inferLeastForUnsat != null) {
        StratifiedInlining = 1;
      }

      if (StratifiedInlining > 0) {
        TypeEncodingMethod = TypeEncoding.Monomorphic;
        UseArrayTheory = true;
        UseAbstractInterpretation = false;
        MaxProverMemory = 0; // no max: avoids restarts
        if (ProverName == "Z3API" || ProverName == "SMTLIB") {
          ProverCCLimit = 1;
        }
      }

      if (Trace) {
        BoogieDebug.DoPrinting = true;  // reuse the -trace option for debug printing
      }
    }



    public bool UserWantsToCheckRoutine(string methodFullname) {
      Contract.Requires(methodFullname != null);
      if (ProcsToCheck == null) {
        // no preference
        return true;
      }
      return Contract.Exists(ProcsToCheck, s => 0 <= methodFullname.IndexOf(s));
    }

    public virtual StringCollection ParseNamedArgumentList(string argList) {
      if (argList == null || argList.Length == 0)
        return null;
      StringCollection result = new StringCollection();
      int i = 0;
      for (int n = argList.Length; i < n; ) {
        cce.LoopInvariant(0 <= i);
        int separatorIndex = this.GetArgumentSeparatorIndex(argList, i);
        if (separatorIndex > i) {
          result.Add(argList.Substring(i, separatorIndex - i));
          i = separatorIndex + 1;
          continue;
        }
        result.Add(argList.Substring(i));
        break;
      }
      return result;
    }
    public int GetArgumentSeparatorIndex(string argList, int startIndex) {
      Contract.Requires(argList != null);
      Contract.Requires(0 <= startIndex && startIndex <= argList.Length);
      Contract.Ensures(Contract.Result<int>() < argList.Length);
      int commaIndex = argList.IndexOf(",", startIndex);
      int semicolonIndex = argList.IndexOf(";", startIndex);
      if (commaIndex == -1)
        return semicolonIndex;
      if (semicolonIndex == -1)
        return commaIndex;
      if (commaIndex < semicolonIndex)
        return commaIndex;
      return semicolonIndex;
    }

    public override void AttributeUsage() {
      Console.WriteLine(
@"Boogie: The following attributes are supported by this implementation.

  ---- On top-level declarations ---------------------------------------------

    {:ignore}
      Ignore the declaration (after checking for duplicate names).

    {:extern}
      If two top-level declarations introduce the same name (for example, two
      constants with the same name or two procedures with the same name), then
      Boogie usually produces an error message.  However, if at least one of
      the declarations is declared with :extern, one of the declarations is
      ignored.  If both declarations are :extern, Boogie arbitrarily chooses
      one of them to keep; otherwise, Boogie ignore the :extern declaration
      and keeps the other.

  ---- On implementations and procedures -------------------------------------

     {:inline N}
       Inline given procedure (can be also used on implementation).
       N should be a non-negative number and represents the inlining depth.
       With /inline:assume call is replaced with ""assume false"" once inlining depth is reached.
       With /inline:assert call is replaced with ""assert false"" once inlining depth is reached.
       With /inline:spec call is left as is once inlining depth is reached.
       With the above three options, methods with the attribute {:inline N} are not verified.
       With /inline:none the entire attribute is ignored.

     {:verify false}
       Skip verification of an implementation.

     {:vcs_max_cost N}
     {:vcs_max_splits N}
     {:vcs_max_keep_going_splits N}
       Per-implementation versions of
       /vcsMaxCost, /vcsMaxSplits and /vcsMaxKeepGoingSplits.

     {:selective_checking true}
       Turn all asserts into assumes except for the ones reachable from
       assumptions marked with the attribute {:start_checking_here}.
       Thus, ""assume {:start_checking_here} something;"" becomes an inverse
       of ""assume false;"": the first one disables all verification before
       it, and the second one disables all verification after.

  ---- On functions ----------------------------------------------------------

     {:builtin ""spec""}
     {:bvbuiltin ""spec""}
       Rewrite the function to built-in prover function symbol 'fn'.

     {:inline}
     {:inline true}
       Expand function according to its definition before going to the prover.

     {:never_pattern true}
       Terms starting with this function symbol will never be
       automatically selected as patterns. It does not prevent them
       from being used inside the triggers, and does not affect explicit
       trigger annotations. Internally it works by adding {:nopats ...}
       annotations to quantifiers.

  ---- On variables ----------------------------------------------------------

     {:existential true}
       Marks a global Boolean variable as existentially quantified. If
       used in combination with option /contractInfer Boogie will check
       whether there exists a Boolean assignment to the existentials
       that makes all verification conditions valid.  Without option
       /contractInfer the attribute is ignored.

  ---- On assert statements --------------------------------------------------

     {:subsumption n}
       Overrides the /subsumption command-line setting for this assertion.

  ---- The end ---------------------------------------------------------------
");
    }

    public override void Usage() {
      Console.WriteLine(@"
  /nologo       suppress printing of version number, copyright message
  /env:<n>      print command line arguments
                  0 - never, 1 (default) - during BPL print and prover log,
                  2 - like 1 and also to standard output
  /wait         await Enter from keyboard before terminating program
  /xml:<file>   also produce output in XML format to <file>

  ---- Boogie options --------------------------------------------------------

  Multiple .bpl files supplied on the command line are concatenated into one
  Boogie program.

  /proc:<p>      : limits which procedures to check
  /noResolve     : parse only
  /noTypecheck   : parse and resolve only

  /print:<file>  : print Boogie program after parsing it
                   (use - as <file> to print to console)
  /printWithUniqueIds : print augmented information that uniquely
                   identifies variables
  /printUnstructured : with /print option, desugars all structured statements
  /printDesugared : with /print option, desugars calls

  /overlookTypeErrors : skip any implementation with resolution or type
                        checking errors

  /loopUnroll:<n>
                unroll loops, following up to n back edges (and then some)
  /printModel:<n>
                0 (default) - do not print Z3's error model
                1 - print Z3's error model
                2 - print Z3's error model plus reverse mappings
                4 - print Z3's error model in a more human readable way
  /printModelToFile:<file>
                print model to <file> instead of console
  /mv:<file>    Specify file where to save the model in BVD format
  /enhancedErrorMessages:<n>
                0 (default) - no enhanced error messages
                1 - Z3 error model enhanced error messages

  ---- Inference options -----------------------------------------------------

  /infer:<flags>
                use abstract interpretation to infer invariants
                The default is /infer:i"
        // This is not 100% true, as the /infer ALWAYS creates
        // a multilattice, whereas if nothing is specified then
        // intervals are isntantiated WITHOUT being embedded in
        // a multilattice
                                          + @"
                   <flags> are as follows (missing <flags> means all)
                   i = intervals
                   c = constant propagation
                   d = dynamic type
                   n = nullness
                   p = polyhedra for linear inequalities
                   t = trivial bottom/top lattice (cannot be combined with
                       other domains)
                   j = stronger intervals (cannot be combined with other
                       domains)
                or the following (which denote options, not domains):
                   s = debug statistics
                0..9 = number of iterations before applying a widen (default=0)
  /noinfer      turn off the default inference, and overrides the /infer
                switch on its left
  /checkInfer   instrument inferred invariants as asserts to be checked by
                theorem prover
  /interprocInfer
                perform interprocedural inference (deprecated, not supported)
  /contractInfer
                perform procedure contract inference
  /logInfer     print debug output during inference
  /instrumentInfer
                h - instrument inferred invariants only at beginning of
                    loop headers (default)
                e - instrument inferred invariants at beginning and end
                    of every block (this mode is intended for use in
                    debugging of abstract domains)
  /printInstrumented
                print Boogie program after it has been instrumented with
                invariants

  ---- Debugging and general tracing options ---------------------------------

  /trace        blurt out various debug trace information
  /traceTimes   output timing information at certain points in the pipeline
  /log[:method] Print debug output during translation

  /break        launch and break into debugger

  ---- Verification-condition generation options -----------------------------

  /liveVariableAnalysis:<c>
                0 = do not perform live variable analysis
                1 = perform live variable analysis (default)
                2 = perform interprocedural live variable analysis
  /noVerify     skip VC generation and invocation of the theorem prover
  /removeEmptyBlocks:<c>
                0 - do not remove empty blocks during VC generation
                1 - remove empty blocks (default)
  /coalesceBlocks:<c>
                0 = do not coalesce blocks
                1 = coalesce blocks (default)
  /vc:<variety> n = nested block (default for /prover:Simplify),
                m = nested block reach,
                b = flat block, r = flat block reach,
                s = structured, l = local,
                d = dag (default, except with /prover:Simplify)
                doomed = doomed
  /traceverify  print debug output during verification condition generation
  /subsumption:<c>
                apply subsumption to asserted conditions:
                0 - never, 1 - not for quantifiers, 2 (default) - always
  /alwaysAssumeFreeLoopInvariants
                usually, a free loop invariant (or assume
                statement in that position) is ignored in checking contexts
                (like other free things); this option includes these free
                loop invariants as assumes in both contexts
  /inline:<i>   use inlining strategy <i> for procedures with the :inline
                attribute, see /attrHelp for details:
                  none
                  assume (default)
                  assert
                  spec
  /printInlined
                print the implementation after inlining calls to
                procedures with the :inline attribute (works with /inline)
  /lazyInline:1
                Use the lazy inlining algorithm
  /stratifiedInline:1
                Use the stratified inlining algorithm
  /recursionBound:<n>
                Set the recursion bound for stratified inlining to
                be n (default 500)
  /inferLeastForUnsat:<str>
                Infer the least number of constants (whose names
                are prefixed by <str>) that need to be set to
                true for the program to be correct. This turns
                on stratified inlining.
  /smoke        Soundness Smoke Test: try to stick assert false; in some
                places in the BPL and see if we can still prove it
  /smokeTimeout:<n>
                Timeout, in seconds, for a single theorem prover
                invocation during smoke test, defaults to 10.
  /causalImplies
                Translate Boogie's A ==> B into prover's A ==> A && B.
  /typeEncoding:<m>
                how to encode types when sending VC to theorem prover
                   n = none (unsound)
                   p = predicates (default)
                   a = arguments
  /monomorphize   
                Do not abstract map types in the encoding (this is an
                experimental feature that will not do the right thing if
                the program uses polymorphism)
  /reflectAdd   In the VC, generate an auxiliary symbol, elsewhere defined
                to be +, instead of +.

  ---- Verification-condition splitting --------------------------------------

  /vcsMaxCost:<f>
                VC will not be split unless the cost of a VC exceeds this
                number, defaults to 2000.0. This does NOT apply in the
                keep-going mode after first round of splitting.
  /vcsMaxSplits:<n>
                Maximal number of VC generated per method. In keep
                going mode only applies to the first round.
                Defaults to 1.
  /vcsMaxKeepGoingSplits:<n>
                If set to more than 1, activates the keep
                going mode, where after the first round of splitting,
                VCs that timed out are split into <n> pieces and retried
                until we succeed proving them, or there is only one
                assertion on a single path and it timeouts (in which
                case error is reported for that assertion).
                Defaults to 1.
  /vcsKeepGoingTimeout:<n>
                Timeout in seconds for a single theorem prover
                invocation in keep going mode, except for the final
                single-assertion case. Defaults to 1s.
  /vcsFinalAssertTimeout:<n>
                Timeout in seconds for the single last
                assertion in the keep going mode. Defaults to 30s.
  /vcsPathJoinMult:<f>
                If more than one path join at a block, by how much
                multiply the number of paths in that block, to accomodate
                for the fact that the prover will learn something on one
                paths, before proceeding to another. Defaults to 0.8.
  /vcsPathCostMult:<f1>
  /vcsAssumeMult:<f2>
                The cost of a block is
                    (<assert-cost> + <f2>*<assume-cost>) * 
                    (1.0 + <f1>*<entering-paths>)
                <f1> defaults to 1.0, <f2> defaults to 0.01.
                The cost of a single assertion or assumption is
                currently always 1.0.
  /vcsPathSplitMult:<f>
                If the best path split of a VC of cost A is into
                VCs of cost B and C, then the split is applied if
                A >= <f>*(B+C), otherwise assertion splitting will be
                applied. Defaults to 0.5 (always do path splitting if
                possible), set to more to do less path splitting
                and more assertion splitting.
  /vcsDumpSplits
                For split #n dump split.n.dot and split.n.bpl.
                Warning: Affects error reporting.
  /vcsCores:<n>
                Try to verify <n> VCs at once. Defaults to 1.

  ---- Prover options --------------------------------------------------------

  /errorLimit:<num>
                Limit the number of errors produced for each procedure
                (default is 5, some provers may support only 1)
  /timeLimit:<num>
                Limit the number of seconds spent trying to verify
                each procedure
  /errorTrace:<n>
                0 - no Trace labels in the error output,
                1 (default) - include useful Trace labels in error output,
                2 - include all Trace labels in the error output
  /vcBrackets:<b>
                bracket odd-charactered identifier names with |'s.  <b> is:
                   0 - no (default with non-/prover:Simplify),
                   1 - yes (default with /prover:Simplify)
  /prover:<tp>  use theorem prover <tp>, where <tp> is either the name of
                a DLL containing the prover interface located in the
                Boogie directory, or a full path to a DLL containing such
                an interface. The standard interfaces shipped include:
                    SMTLib (default, uses the SMTLib2 format and calls Z3)
                    Z3 (uses Z3 with the Simplify format)
                    Simplify
                    ContractInference (uses Z3)
                    Z3api (Z3 using Managed .NET API)
  /proverOpt:KEY[=VALUE]
                Provide a prover-specific option (short form /p).
  /proverLog:<file>
                Log input for the theorem prover.  Like filenames
                supplied as arguments to other options, <file> can use the
                following macros:
                    @TIME@    expands to the current time
                    @PREFIX@  expands to the concatenation of strings given
                              by /logPrefix options
                    @FILE@    expands to the last filename specified on the
                              command line
                In addition, /proverLog can also use the macro '@PROC@',
                which causes there to be one prover log file per
                verification condition, and the macro then expands to the
                name of the procedure that the verification condition is for.
  /logPrefix:<str>
                Defines the expansion of the macro '@PREFIX@', which can
                be used in various filenames specified by other options.
  /proverLogAppend
                Append (not overwrite) the specified prover log file
  /proverWarnings
                0 (default) - don't print, 1 - print to stdout,
                2 - print to stderr
  /proverMemoryLimit:<num>
                Limit on the virtual memory for prover before
                restart in MB (default:100MB)
  /restartProver
                Restart the prover after each query
  /proverShutdownLimit<num>
                Time between closing the stream to the prover and
                killing the prover process (default: 0s)
  /platform:<ptype>,<location>
                ptype = v11,v2,cli1
                location = platform libraries directory

  Simplify specific options:
  /simplifyMatchDepth:<num>
                Set Simplify prover's matching depth limit

  Z3 specific options:
  /z3opt:<arg>  specify additional Z3 options
  /z3multipleErrors
                report multiple counterexamples for each error
  /useArrayTheory
                use Z3's native theory (as opposed to axioms).  Currently
                implies /monomorphize.

  /z3types      generate multi-sorted VC that make use of Z3 types
  /z3lets:<n>   0 - no LETs, 1 - only LET TERM, 2 - only LET FORMULA,
                3 - (default) any
  /z3exe:<path>
                path to Z3 executable
");
    }
  }
}
