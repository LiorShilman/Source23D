using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodeFlow3D.Models;

namespace CodeFlow3D.ViewModels
{
    public partial class SimulatorViewModel : ObservableObject
    {
        public event EventHandler<SimulationStep> StepChanged;

        private DispatcherTimer _timer;

        [ObservableProperty]
        private SimulationSession _session;

        [ObservableProperty]
        private int _currentStep;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private double _playbackSpeed = 1.5;

        public int TotalSteps => Session?.Steps?.Count ?? 0;

        public SimulationStep CurrentStepData =>
            Session != null && CurrentStep < TotalSteps
                ? Session.Steps[CurrentStep]
                : null;

        public bool HasSession => Session != null;

        public void StopPlayback()
        {
            IsPlaying = false;
        }

        public void LoadSession(SimulationSession session)
        {
            Session = session;
            CurrentStep = 0;
            IsPlaying = false;
            OnPropertyChanged(nameof(TotalSteps));
            OnPropertyChanged(nameof(HasSession));
            NotifyStep();
        }

        partial void OnCurrentStepChanged(int value)
        {
            OnPropertyChanged(nameof(CurrentStepData));
            NotifyStep();
        }

        partial void OnIsPlayingChanged(bool value)
        {
            if (value) StartTimer();
            else StopTimer();
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Tick += (s, e) =>
                {
                    if (CurrentStep < TotalSteps - 1)
                        CurrentStep++;
                    else
                        IsPlaying = false;
                };
            }
            _timer.Interval = TimeSpan.FromMilliseconds(900 / PlaybackSpeed);
            _timer.Start();
        }

        private void StopTimer()
        {
            _timer?.Stop();
        }

        private void NotifyStep()
        {
            StepChanged?.Invoke(this, CurrentStepData);
        }

        [RelayCommand]
        private void TogglePlay()
        {
            if (Session == null) return;
            if (IsPlaying)
            {
                IsPlaying = false;
            }
            else
            {
                if (CurrentStep >= TotalSteps - 1)
                    CurrentStep = 0;
                IsPlaying = true;
            }
        }

        [RelayCommand]
        private void StepForward()
        {
            IsPlaying = false;
            if (CurrentStep < TotalSteps - 1)
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
        private void Restart()
        {
            IsPlaying = false;
            CurrentStep = 0;
        }
    }
}
