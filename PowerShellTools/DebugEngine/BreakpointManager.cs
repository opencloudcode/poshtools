﻿using log4net;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using PowerShellTools.Common.ServiceManagement.DebuggingContract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading.Tasks;

namespace PowerShellTools.DebugEngine
{
    public class BreakpointManager
    {
        private List<ScriptBreakpoint> _breakpoints;
        private ScriptDebugger _debugger;
        private static readonly ILog Log = LogManager.GetLogger(typeof(BreakpointManager));

        /// <summary>
        /// Event is fired when a breakpoint is hit.
        /// </summary>
        public event EventHandler<EventArgs<ScriptBreakpoint>> BreakpointHit;

        /// <summary>
        /// Event is fired when a breakpoint is updated.
        /// </summary>
        public event EventHandler<DebuggerBreakpointUpdatedEventArgs> BreakpointUpdated;

        public ScriptDebugger Debugger{
            get 
            {
                if(_debugger == null)
                    return PowerShellToolsPackage.Debugger;

                return _debugger;
            }
        }

        /// <summary>
        /// Ctor
        /// </summary>
        public BreakpointManager()
        {
            _breakpoints = new List<ScriptBreakpoint>();
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="debugger">Script debugger</param>
        public BreakpointManager(ScriptDebugger debugger) 
            : this()
        {
            _debugger = debugger;
        }

        /// <summary>
        /// Sets breakpoints for the current runspace.
        /// </summary>
        /// <remarks>
        /// This method clears any existing breakpoints.
        /// </remarks>
        /// <param name="initialBreakpoints"></param>
        public void SetBreakpoints(IEnumerable<ScriptBreakpoint> initialBreakpoints)
        {
            if (initialBreakpoints == null) return;

            Log.InfoFormat("ScriptDebugger: Initial Breakpoints: {0}", initialBreakpoints.Count());
            ClearBreakpoints();

            foreach (var bp in initialBreakpoints)
            {
                SetBreakpoint(bp);
                _breakpoints.Add(bp);

                enum_BP_STATE[] pState = new enum_BP_STATE[1];
                if (bp.GetState(pState) == VSConstants.S_OK)
                {
                    if (pState[0] == enum_BP_STATE.BPS_DISABLED)
                    {
                        EnableBreakpoint(bp, 0);  // Disable PS breakpoint
                    }
                }
            }
        }
        
        /// <summary>
        /// Breakpoint has been updated
        /// </summary>
        /// <param name="e"></param>
        public void UpdateBreakpoint(DebuggerBreakpointUpdatedEventArgs e)
        {
            Log.InfoFormat("Breakpoint updated: {0} {1}", e.UpdateType, e.Breakpoint);

            if (BreakpointUpdated != null)
            {
                BreakpointUpdated(this, e);
            }
        }

        /// <summary>
        /// Process line breakpoint when break
        /// </summary>
        /// <param name="script">Script file</param>
        /// <param name="line">Line of breakpoint</param>
        /// <param name="column">Column of breakpoint</param>
        /// <returns>Success or not</returns>
        public bool ProcessLineBreakpoints(string script, int line, int column)
        {
            Log.InfoFormat("Process Line Breapoints");

            var bp =
                _breakpoints.FirstOrDefault(
                    m =>
                    m.Column == column && line == m.Line &&
                    script.Equals(m.File, StringComparison.InvariantCultureIgnoreCase));

            if (bp != null)
            {
                if (BreakpointHit != null)
                {
                    Log.InfoFormat("Breakpoint @ {0} {1} {2} was hit.", bp.File, bp.Line, bp.Column);
                    BreakpointHit(this, new EventArgs<ScriptBreakpoint>(bp));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Add breakpoint
        /// </summary>
        /// <param name="breakpoint">Breakpoint to be added</param>
        public void SetBreakpoint(ScriptBreakpoint breakpoint)
        {
            Log.InfoFormat("SetBreakpoint: {0} {1} {2}", breakpoint.File, breakpoint.Line, breakpoint.Column);

            try
            {
                if (Debugger.DebuggingService.GetRunspaceAvailability() == RunspaceAvailability.Available)
                {
                    Debugger.DebuggingService.SetBreakpoint(new PowershellBreakpoint(breakpoint.File, breakpoint.Line, breakpoint.Column));
                }
                else
                {
                    Debugger.ExecuteDebuggingCommand(string.Format("Set-PSBreakpoint -Script \"{0}\" -Line {1}", breakpoint.File, breakpoint.Line));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to set breakpoint.", ex);
            }
        }

        /// <summary>
        /// Enable breakpoint
        /// </summary>
        /// <param name="breakpoint">Breakpoint to be added</param>
        public void EnableBreakpoint(ScriptBreakpoint breakpoint, int fEnable)
        {
            Log.InfoFormat("EnableBreakpoint: {0} {1} {2}", breakpoint.File, breakpoint.Line, breakpoint.Column);

            try
            {
                if (Debugger.DebuggingService.GetRunspaceAvailability() == RunspaceAvailability.Available)
                {
                    Debugger.DebuggingService.EnableBreakpoint(new PowershellBreakpoint(breakpoint.File, breakpoint.Line, breakpoint.Column), fEnable == 0 ? false : true);
                }
                else
                {
                    int id = Debugger.DebuggingService.GetPSBreakpointId(new PowershellBreakpoint(breakpoint.File, breakpoint.Line, breakpoint.Column));
                    if (id >= 0)
                    {
                        Debugger.ExecuteDebuggingCommand(
                            string.Format(
                                "{0} -Id {1}",
                                fEnable == 0 ? "Disable-PSBreakpoint" : "Enable-PSBreakpoint",
                                id));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to enable breakpoint.", ex);
            }
        }

        /// <summary>
        /// Enable breakpoint
        /// </summary>
        /// <param name="breakpoint">Breakpoint to be added</param>
        public void RemoveBreakpoint(ScriptBreakpoint breakpoint)
        {
            Log.InfoFormat("RemoveBreakpoint: {0} {1} {2}", breakpoint.File, breakpoint.Line, breakpoint.Column);

            try
            {
                if (Debugger.DebuggingService.GetRunspaceAvailability() == RunspaceAvailability.Available)
                {
                    Debugger.DebuggingService.RemoveBreakpoint(new PowershellBreakpoint(breakpoint.File, breakpoint.Line, breakpoint.Column));
                }
                else
                {
                    int id = Debugger.DebuggingService.GetPSBreakpointId(new PowershellBreakpoint(breakpoint.File, breakpoint.Line, breakpoint.Column));
                    if (id >= 0)
                    {
                        Debugger.ExecuteDebuggingCommand(string.Format("Remove-PSBreakpoint -Id {0}", id));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to remove breakpoint.", ex);
            }
        }

        /// <summary>
        /// Clears existing breakpoints for the current runspace.
        /// </summary>
        public void ClearBreakpoints()
        {
            try
            {
                Debugger.DebuggingService.ClearBreakpoints();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to clear existing breakpoints", ex);
            }
        }
    }
}
