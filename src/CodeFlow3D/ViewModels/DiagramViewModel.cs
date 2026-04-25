using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeFlow3D.Models;

namespace CodeFlow3D.ViewModels
{
    public partial class DiagramViewModel : ObservableObject
    {
        public event EventHandler<SymbolNode> NodeClicked;

        private DispatcherTimer _playTimer;

        [ObservableProperty]
        private ObservableCollection<FlowPath> _paths = new ObservableCollection<FlowPath>();

        [ObservableProperty]
        private FlowPath _selectedPath;

        [ObservableProperty]
        private int _selectedPathIndex;

        [ObservableProperty]
        private DiagramLayout _currentLayout;

        [ObservableProperty]
        private int _currentStep;

        [ObservableProperty]
        private int _totalSteps;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private double _playbackSpeed = 1.0;

        [ObservableProperty]
        private int _maxDepthFilter = 20;

        [ObservableProperty]
        private bool _showReturnArrows = true;

        [ObservableProperty]
        private bool _showAsyncHighlight = true;

        public void LoadPaths(List<FlowPath> paths)
        {
            Paths.Clear();
            foreach (var p in paths)
                Paths.Add(p);

            if (paths.Count > 0)
            {
                SelectedPathIndex = 0;
                SelectedPath = paths[0];
                BuildLayout(paths[0]);
            }
        }

        partial void OnSelectedPathChanged(FlowPath value)
        {
            if (value != null)
                BuildLayout(value);
        }

        partial void OnIsPlayingChanged(bool value)
        {
            if (value)
                StartPlayTimer();
            else
                StopPlayTimer();
        }

        private void StartPlayTimer()
        {
            if (_playTimer == null)
            {
                _playTimer = new DispatcherTimer();
                _playTimer.Tick += (s, e) =>
                {
                    if (CurrentStep < TotalSteps)
                    {
                        CurrentStep++;
                    }
                    else
                    {
                        IsPlaying = false;
                    }
                };
            }
            _playTimer.Interval = TimeSpan.FromMilliseconds(1200 / PlaybackSpeed);
            _playTimer.Start();
        }

        private void StopPlayTimer()
        {
            _playTimer?.Stop();
        }

        public void RaiseNodeClicked(SymbolNode node)
        {
            NodeClicked?.Invoke(this, node);
        }

        private void BuildLayout(FlowPath path)
        {
            StopPlayTimer();
            IsPlaying = false;

            var layout = new DiagramLayout();
            var participantMap = new Dictionary<string, int>();
            int xIndex = 0;

            foreach (var step in path.Steps)
            {
                var key = step.Node.ContainingType ?? step.Node.FilePath ?? step.Node.Id;
                if (!participantMap.ContainsKey(key))
                {
                    participantMap[key] = xIndex;
                    layout.Participants.Add(new ParticipantLayout
                    {
                        Id = key,
                        Label = step.Node.ContainingType ?? System.IO.Path.GetFileName(step.Node.FilePath ?? ""),
                        FilePath = step.Node.FilePath,
                        XPosition = xIndex * 6.0,
                        IsSource = xIndex == 0,
                        IsTarget = false
                    });
                    xIndex++;
                }
            }

            if (layout.Participants.Count > 0)
                layout.Participants.Last().IsTarget = true;

            for (int i = 0; i < path.Steps.Count - 1; i++)
            {
                var caller = path.Steps[i];
                var callee = path.Steps[i + 1];
                var callerKey = caller.Node.ContainingType ?? caller.Node.FilePath ?? caller.Node.Id;
                var calleeKey = callee.Node.ContainingType ?? callee.Node.FilePath ?? callee.Node.Id;

                double callerX = participantMap.ContainsKey(callerKey) ? participantMap[callerKey] * 6.0 : 0;
                double calleeX = participantMap.ContainsKey(calleeKey) ? participantMap[calleeKey] * 6.0 : 0;
                double y = -i * 2.0;
                double z = 1.2 + caller.NestingDepth * 1.5; // offset in front of pillars

                // Arrows start/end at pillar edges, not centers
                double pillarW = 0.7;
                double startX, endX;
                bool isSelfCall = Math.Abs(calleeX - callerX) < 0.01;

                if (isSelfCall)
                {
                    // Self-call: small loop arrow going right from pillar edge
                    startX = callerX + pillarW + 0.1;
                    endX = callerX + pillarW + 1.6;
                }
                else
                {
                    double dir = calleeX > callerX ? 1 : -1;
                    startX = callerX + dir * (pillarW + 0.1);
                    endX = calleeX - dir * (pillarW + 0.1);
                }

                layout.Arrows.Add(new ArrowLayout
                {
                    CallerId = callerKey,
                    CalleeId = calleeKey,
                    Label = callee.Node.Name + "()",
                    SequenceIndex = i,
                    YPosition = y,
                    ZDepth = z,
                    IsReturn = isSelfCall,  // reuse IsReturn flag for self-call indicator
                    IsAsync = callee.Node.IsAsync,
                    StartPoint = new Point3D(startX, y, z),
                    EndPoint = new Point3D(endX, y, z),
                    CallerNode = caller.Node,
                    CalleeNode = callee.Node
                });
            }

            layout.TotalWidth = xIndex * 6.0;
            layout.TotalHeight = path.Steps.Count * 2.0;
            layout.TotalDepth = path.Steps.Max(s => s.NestingDepth) * 1.5;

            TotalSteps = layout.Arrows.Count;
            CurrentStep = 0;
            CurrentLayout = layout;
        }

        [RelayCommand]
        private void TogglePlay()
        {
            if (IsPlaying)
            {
                IsPlaying = false;
            }
            else
            {
                if (CurrentStep >= TotalSteps)
                    CurrentStep = 0;
                IsPlaying = true;
            }
        }

        [RelayCommand]
        private void StepForward()
        {
            IsPlaying = false;
            if (CurrentStep < TotalSteps)
                CurrentStep++;
        }

        [RelayCommand]
        private void StepBack()
        {
            IsPlaying = false;
            if (CurrentStep > 0)
                CurrentStep--;
        }

        [RelayCommand]
        private void JumpToStep(int step)
        {
            if (step >= 0 && step <= TotalSteps)
                CurrentStep = step;
        }
    }
}
