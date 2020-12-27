﻿using System;
using System.Runtime.InteropServices.WindowsRuntime;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen1 : Amd17hCpu
    {
        public Zen1()
        {
            monitoringConfigs = new MonitoringConfig[2];
            monitoringConfigs[0] = new BpuMonitoringConfig(this);
            monitoringConfigs[1] = new DCMonitoringConfig(this);
            architectureName = "Zen 1";
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private Zen1 cpu;
            public string GetConfigName() { return "Branch Prediction and Fusion"; }

            public BpuMonitoringConfig(Zen1 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // PERF_CTR0 = active cycles
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x76, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR1 = retired instructions
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xC0, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR2 to count retired branches
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xC2, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = mispredicted retired branches
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xC3, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR4 = L1 BTB Overrides
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x8A, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR5 = L2 BTB overrides
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x8B, 0, true, true, false, false, true, false, 0, 0, false, false));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Cycles Not In Halt", "Instructions Retired", "Ret Branches", "Ret Misp Branches", "L1 BTB Overrides", "L2 BTB Overrides");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Power", "Instr/Watt",
                "BPU Accuracy", "BPU MPKI", "L1 BTB Overrides/Ki", "L2 BTB Overrides/Ki", "% Branches" };
            
            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float cycles = counterData.ctr0;
                float instr = counterData.ctr1;
                float branches = counterData.ctr2;
                float mispBranches = counterData.ctr3;
                float l1BtbOverrides = counterData.ctr4;
                float l2BtbOverrides = counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        string.Format("{0:F2} W", counterData.watts),
                        FormatLargeNumber(instr / counterData.watts),
                        string.Format("{0:F2}%", 100 * (1 - mispBranches / branches)), // BPU Acc
                        string.Format("{0:F2}", mispBranches / instr * 1000),     // BPU MPKI
                        string.Format("{0:F2}", l1BtbOverrides / instr * 1000),      // L1 BTB Overrides
                        string.Format("{0:F2}", l2BtbOverrides / instr * 1000),      // L2 BTB Overrides
                        string.Format("{0:F2}%", branches / instr * 100) };   // Branch %
            }
        }

        public class DCMonitoringConfig : MonitoringConfig
        {
            private Zen1 cpu;
            public string GetConfigName() { return "DC Refills"; }

            public DCMonitoringConfig(Zen1 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // PERF_CTR2 = active cycles
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x76, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = ret instr
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xC0, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // Set PERF_CTR2 to count DC reflls from L2
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x43, 1, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = DC refills from another cache (L3)
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x43, 2, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR4 = DC refills  from local dram
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x43, 4, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR5 = remote refills
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x43, 0x50, true, true, false, false, true, false, 0, 0, false, false));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Cycles Not In Halt", "Retired Instr", "DC Fill From L2", "DC Fill From Cache", "DC Fill From DRAM", "DC Fill From Remote Cache or DRAM");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L2 to DC Fill BW", "L3 to DC Fill BW", "DRAM to DC Fill BW", "Remote to DC Fill BW" };

            public string GetHelpText()
            {
                return "Zen 1 APERF/IrPerfCount being reset by something else?";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.ctr0),
                        FormatLargeNumber(counterData.ctr1),
                        string.Format("{0:F2}", counterData.ctr1 / counterData.ctr0),
                        FormatLargeNumber(counterData.ctr2 * 64) + "B/s",
                        FormatLargeNumber(counterData.ctr3 * 64) + "B/s",
                        FormatLargeNumber(counterData.ctr4 * 64) + "B/s",
                        FormatLargeNumber(counterData.ctr5 * 64) + "B/s",
                };
            }
        }
    }
}
