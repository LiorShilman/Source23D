using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CodeFlow3D.Models;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D.Views
{
    public partial class DiagramPanel : UserControl
    {
        private const int TubeSegments = 10;
        private const double TubeRadius = 0.035;
        private const double SelfCallTubeRadius = 0.02;
        private const double ConeRadius = 0.11;
        private const double ConeLength = 0.28;
        private const double PillarWidth = 0.7;

        /// <summary>Each arrow step gets its own visual group (tube+glow+cone+dot+labels).</summary>
        private readonly List<ArrowStepVisuals> _arrowSteps = new List<ArrowStepVisuals>();

        /// <summary>Maps GeometryModel3D → SymbolNode for click-to-code hit testing.</summary>
        private readonly Dictionary<Model3D, SymbolNode> _modelToNode = new Dictionary<Model3D, SymbolNode>();

        /// <summary>Stores original text for billboard labels (for show/hide during playback).</summary>
        private readonly Dictionary<BillboardTextVisual3D, string> _labelOrigText = new Dictionary<BillboardTextVisual3D, string>();

        /// <summary>Stores original materials for hover highlight restore.</summary>
        private readonly Dictionary<GeometryModel3D, Material> _originalMaterials = new Dictionary<GeometryModel3D, Material>();

        /// <summary>Currently highlighted model (for un-highlighting on mouse move).</summary>
        private GeometryModel3D _hoveredModel;

        /// <summary>Tooltip popup for hovered functions.</summary>
        private readonly ToolTip _hoverTip = new ToolTip
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 17, 25)),
            Foreground = new SolidColorBrush(Color.FromRgb(200, 220, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 255)),
            BorderThickness = new Thickness(1),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(10, 6, 10, 6),
        };

        public DiagramPanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Viewport3D.MouseLeftButtonDown += OnViewportClick;
            Viewport3D.MouseMove += OnViewportMouseMove;
            Viewport3D.MouseLeave += OnViewportMouseLeave;

            // Re-fit the camera whenever this panel is made visible.
            // ZoomExtents only works on a panel with a real size, so if the panel
            // was Collapsed when RenderScene ran (e.g. simulator mode was active),
            // we need to recalculate the camera position now that layout is real.
            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if ((DataContext as DiagramViewModel)?.CurrentLayout != null)
                            Viewport3D.ZoomExtents(300);
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            };
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DiagramViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is DiagramViewModel newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var vm = (DiagramViewModel)sender;
            if (e.PropertyName == nameof(DiagramViewModel.CurrentLayout))
            {
                if (vm.CurrentLayout != null)
                    RenderScene(vm.CurrentLayout);
            }
            else if (e.PropertyName == nameof(DiagramViewModel.CurrentStep))
            {
                UpdateStepVisibility(vm.CurrentStep);
            }
        }

        #region Hover Highlight + Click-to-Code

        private SymbolNode HitTestNode(Point pt, out GeometryModel3D hitModel)
        {
            SymbolNode hitNode = null;
            GeometryModel3D foundModel = null;

            VisualTreeHelper.HitTest(
                Viewport3D, null,
                result =>
                {
                    if (result is RayMeshGeometry3DHitTestResult meshHit &&
                        meshHit.ModelHit is GeometryModel3D gm &&
                        _modelToNode.TryGetValue(gm, out var node))
                    {
                        hitNode = node;
                        foundModel = gm;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                new PointHitTestParameters(pt));

            hitModel = foundModel;
            return hitNode;
        }

        private void OnViewportClick(object sender, MouseButtonEventArgs e)
        {
            var pt = e.GetPosition(Viewport3D);
            var node = HitTestNode(pt, out _);
            if (node != null)
            {
                var vm = DataContext as DiagramViewModel;
                vm?.RaiseNodeClicked(node);
                e.Handled = true;
            }
        }

        private void OnViewportMouseMove(object sender, MouseEventArgs e)
        {
            var pt = e.GetPosition(Viewport3D);
            var node = HitTestNode(pt, out var hitModel);

            // Un-highlight previous
            if (_hoveredModel != null && _hoveredModel != hitModel)
            {
                if (_originalMaterials.TryGetValue(_hoveredModel, out var origMat))
                {
                    _hoveredModel.Material = origMat;
                    _hoveredModel.BackMaterial = origMat;
                }
                _hoveredModel = null;
            }

            if (node != null && hitModel != null)
            {
                Cursor = Cursors.Hand;

                // Highlight with bright emissive
                if (_hoveredModel != hitModel)
                {
                    _hoveredModel = hitModel;
                    if (!_originalMaterials.ContainsKey(hitModel))
                        _originalMaterials[hitModel] = hitModel.Material;

                    var highlight = new MaterialGroup();
                    highlight.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.White)));
                    highlight.Children.Add(new DiffuseMaterial(new SolidColorBrush(
                        Color.FromArgb(200, 100, 220, 255))));
                    hitModel.Material = highlight;
                    hitModel.BackMaterial = highlight;
                }

                // Tooltip
                var displayName = node.DisplayName ?? node.Name;
                var filePart = !string.IsNullOrEmpty(node.FilePath)
                    ? $"\n{System.IO.Path.GetFileName(node.FilePath)}:{node.LineStart}"
                    : "";
                _hoverTip.Content = $"{displayName}{filePart}\n\nClick to view code";
                _hoverTip.IsOpen = true;
                _hoverTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            }
            else
            {
                Cursor = Cursors.Arrow;
                _hoverTip.IsOpen = false;
            }
        }

        private void OnViewportMouseLeave(object sender, MouseEventArgs e)
        {
            // Restore highlight
            if (_hoveredModel != null)
            {
                if (_originalMaterials.TryGetValue(_hoveredModel, out var origMat))
                {
                    _hoveredModel.Material = origMat;
                    _hoveredModel.BackMaterial = origMat;
                }
                _hoveredModel = null;
            }
            Cursor = Cursors.Arrow;
            _hoverTip.IsOpen = false;
        }

        #endregion

        #region Step Visibility (Play animation)

        private void UpdateStepVisibility(int currentStep)
        {
            for (int i = 0; i < _arrowSteps.Count; i++)
            {
                var step = _arrowSteps[i];
                bool visible = i < currentStep;

                // Show/hide the 3D model group
                step.ModelVisual.Content = visible ? step.ModelGroup : null;

                // Show/hide billboard labels
                foreach (var label in step.Labels)
                {
                    if (visible && _labelOrigText.TryGetValue(label, out var origText))
                        label.Text = origText;
                    else if (!visible)
                        label.Text = "";
                }
            }
        }

        #endregion

        private void RenderScene(DiagramLayout layout)
        {
            // Clear everything
            OpaqueScene.Children.Clear();
            TransparentScene.Children.Clear();
            DynamicLights.Children.Clear();
            _arrowSteps.Clear();
            _modelToNode.Clear();
            _labelOrigText.Clear();
            _originalMaterials.Clear();
            _hoveredModel = null;
            _hoverTip.IsOpen = false;

            // Remove old billboard labels
            for (int i = Viewport3D.Children.Count - 1; i >= 0; i--)
            {
                if (Viewport3D.Children[i] is BillboardTextVisual3D)
                    Viewport3D.Children.RemoveAt(i);
            }

            // Position grid floor
            double floorY = -(layout.TotalHeight + 3);
            double centerX = layout.TotalWidth / 2.0;
            GridFloor.Center = new Point3D(centerX, floorY, 0);
            GridFloor.Width = layout.TotalWidth + 20;
            GridFloor.Length = 20;
            GridFloor.Normal = new Vector3D(0, 1, 0);

            // Render pillars (transparent → TransparentScene)
            foreach (var participant in layout.Participants)
            {
                var baseColor = GetPillarColor(participant);
                double top = 4.0;
                double bottom = floorY + 0.1;

                // Glass pillar body → transparent scene
                var pillarModel = CreatePillar(participant.XPosition, top, bottom, baseColor);
                var pillarVisual = new ModelVisual3D { Content = pillarModel };
                TransparentScene.Children.Add(pillarVisual);

                // Wireframe edges → opaque scene (they are emissive/bright)
                var wireGroup = new Model3DGroup();
                foreach (var edge in CreatePillarWireframe(participant.XPosition, top, bottom, baseColor))
                    wireGroup.Children.Add(edge);
                var wireVisual = new ModelVisual3D { Content = wireGroup };
                OpaqueScene.Children.Add(wireVisual);

                // Base glow disc
                var discVisual = new ModelVisual3D { Content = CreateDisc(participant.XPosition, floorY + 0.08, 1.0, baseColor, 20) };
                OpaqueScene.Children.Add(discVisual);

                // Top cap disc
                var topDiscVisual = new ModelVisual3D { Content = CreateDisc(participant.XPosition, top + 0.02, PillarWidth + 0.05, baseColor, 35) };
                OpaqueScene.Children.Add(topDiscVisual);

                // Subtle point light
                AddPointLight(new Point3D(participant.XPosition, top + 1, 0), baseColor, 8.0);

                // Pillar name label (always visible)
                AddLabel(participant.Label,
                    new Point3D(participant.XPosition, top + 1.0, 0),
                    baseColor, 14);

                // Role label
                if (participant.IsSource)
                    AddLabel("SOURCE", new Point3D(participant.XPosition, top + 2.0, 0),
                        (Color)FindResource("AccentCyan"), 9);
                else if (participant.IsTarget)
                    AddLabel("TARGET", new Point3D(participant.XPosition, top + 2.0, 0),
                        (Color)FindResource("AccentGold"), 9);
            }

            // Render arrows — each arrow step gets its own ModelVisual3D for show/hide
            foreach (var arrow in layout.Arrows)
            {
                Color arrowColor = arrow.IsAsync
                    ? (Color)FindResource("AccentGreen")
                    : (Color)FindResource("AccentCyan");

                var stepGroup = new Model3DGroup();

                if (arrow.IsReturn) // self-call: small neat U-shaped loop
                {
                    var s = arrow.StartPoint;
                    double loopOut = 1.2;  // how far to the right it extends
                    double loopDown = 0.5; // how far down it drops
                    var corner1 = new Point3D(s.X + loopOut, s.Y, s.Z);
                    var corner2 = new Point3D(s.X + loopOut, s.Y - loopDown, s.Z);
                    var corner3 = new Point3D(s.X, s.Y - loopDown, s.Z);

                    stepGroup.Children.Add(CreateSlimTube(s, corner1, arrowColor));
                    stepGroup.Children.Add(CreateSlimTube(corner1, corner2, arrowColor));
                    stepGroup.Children.Add(CreateSlimTube(corner2, corner3, arrowColor));

                    var returnCone = CreateConeArrowhead(corner2, corner3, arrowColor);
                    stepGroup.Children.Add(returnCone);
                    if (arrow.CalleeNode != null)
                        _modelToNode[returnCone] = arrow.CalleeNode;
                }
                else // normal cross-pillar arrow
                {
                    var tube = CreateTubeArrow(arrow.StartPoint, arrow.EndPoint, arrowColor);
                    stepGroup.Children.Add(tube);
                    if (arrow.CalleeNode != null)
                        _modelToNode[tube] = arrow.CalleeNode;

                    stepGroup.Children.Add(CreateGlowTube(arrow.StartPoint, arrow.EndPoint, arrowColor));

                    var cone = CreateConeArrowhead(arrow.StartPoint, arrow.EndPoint, arrowColor);
                    stepGroup.Children.Add(cone);
                    if (arrow.CalleeNode != null)
                        _modelToNode[cone] = arrow.CalleeNode;
                }

                // Visual container for show/hide
                var stepVisual = new ModelVisual3D { Content = stepGroup };
                OpaqueScene.Children.Add(stepVisual);

                // Arrow function name label (above midpoint, self-calls above loop top)
                Point3D labelPos;
                if (arrow.IsReturn)
                    labelPos = new Point3D(arrow.StartPoint.X + 0.6, arrow.StartPoint.Y + 0.5, arrow.StartPoint.Z);
                else
                    labelPos = new Point3D(
                        (arrow.StartPoint.X + arrow.EndPoint.X) / 2,
                        arrow.StartPoint.Y + 0.55,
                        arrow.StartPoint.Z + 0.2);
                var arrowLabel = CreateBillboard(arrow.Label, labelPos, arrowColor, 11);
                Viewport3D.Children.Add(arrowLabel);

                // Sequence badge — sits just left of arrow start, slightly above
                var badgePos = new Point3D(
                    arrow.StartPoint.X - 0.35,
                    arrow.StartPoint.Y + 0.18,
                    arrow.StartPoint.Z + 0.3);
                var seqLabel = CreateNumberBadge(arrow.SequenceIndex + 1, badgePos, arrowColor);
                Viewport3D.Children.Add(seqLabel);

                _arrowSteps.Add(new ArrowStepVisuals
                {
                    ModelVisual = stepVisual,
                    ModelGroup = stepGroup,
                    Labels = new List<BillboardTextVisual3D> { arrowLabel, seqLabel }
                });
            }

            WelcomeOverlay.Visibility = Visibility.Collapsed;

            // Show all steps initially (CurrentStep = TotalSteps means all visible)
            var vm = DataContext as DiagramViewModel;
            if (vm != null)
            {
                vm.CurrentStep = vm.TotalSteps; // show all
            }

            Viewport3D.ZoomExtents(200);
        }

        #region Labels & Lights

        private void AddLabel(string text, Point3D position, Color foreground, double fontSize)
        {
            Viewport3D.Children.Add(CreateBillboard(text, position, foreground, fontSize));
        }

        private BillboardTextVisual3D CreateBillboard(string text, Point3D position, Color foreground, double fontSize)
        {
            var label = new BillboardTextVisual3D
            {
                Text = text,
                Position = position,
                FontSize = fontSize,
                Foreground = new SolidColorBrush(foreground),
                Background = new SolidColorBrush(Color.FromArgb(160, 10, 12, 20)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, foreground.R, foreground.G, foreground.B)),
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(6, 2, 6, 2),
                FontFamily = new FontFamily("Consolas"),
            };
            // Store original text for show/hide during playback
            _labelOrigText[label] = text;
            return label;
        }

        private BillboardTextVisual3D CreateNumberBadge(int number, Point3D position, Color accentColor)
        {
            var dim = Color.FromArgb(180, (byte)(accentColor.R / 3), (byte)(accentColor.G / 3), (byte)(accentColor.B / 3));
            var label = new BillboardTextVisual3D
            {
                Text = number.ToString(),
                Position = position,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(dim),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(0.8),
                Padding = new Thickness(4, 1, 4, 1),
                FontFamily = new FontFamily("Consolas"),
            };
            _labelOrigText[label] = number.ToString();
            return label;
        }

        private void AddPointLight(Point3D position, Color color, double range)
        {
            var light = new PointLight
            {
                Color = Color.FromArgb(100, color.R, color.G, color.B),
                Position = position,
                Range = range,
                ConstantAttenuation = 0.5,
                LinearAttenuation = 0.3,
                QuadraticAttenuation = 0.1
            };
            DynamicLights.Children.Add(new ModelVisual3D { Content = light });
        }

        #endregion

        #region Pillars

        private Color GetPillarColor(ParticipantLayout participant)
        {
            if (participant.IsSource)
                return (Color)FindResource("AccentCyan");
            if (participant.IsTarget)
                return (Color)FindResource("AccentGold");
            return (Color)FindResource("AccentPurple");
        }

        private GeometryModel3D CreatePillar(double x, double top, double bottom, Color baseColor)
        {
            var mesh = new MeshGeometry3D();
            double w = PillarWidth;

            var p0 = new Point3D(x - w, top, -w);
            var p1 = new Point3D(x + w, top, -w);
            var p2 = new Point3D(x + w, top, w);
            var p3 = new Point3D(x - w, top, w);
            var p4 = new Point3D(x - w, bottom, -w);
            var p5 = new Point3D(x + w, bottom, -w);
            var p6 = new Point3D(x + w, bottom, w);
            var p7 = new Point3D(x - w, bottom, w);

            AddQuad(mesh, p0, p1, p5, p4);
            AddQuad(mesh, p2, p3, p7, p6);
            AddQuad(mesh, p3, p0, p4, p7);
            AddQuad(mesh, p1, p2, p6, p5);
            AddQuad(mesh, p0, p3, p2, p1);
            AddQuad(mesh, p4, p5, p6, p7);

            var glass = new MaterialGroup();
            glass.Children.Add(new DiffuseMaterial(new SolidColorBrush(
                Color.FromArgb(30, baseColor.R, baseColor.G, baseColor.B))));
            glass.Children.Add(new SpecularMaterial(new SolidColorBrush(
                Color.FromArgb(140, 255, 255, 255)), 120));
            glass.Children.Add(new EmissiveMaterial(new SolidColorBrush(
                Color.FromArgb(15, baseColor.R, baseColor.G, baseColor.B))));

            return new GeometryModel3D(mesh, glass) { BackMaterial = glass };
        }

        private List<GeometryModel3D> CreatePillarWireframe(double x, double top, double bottom, Color baseColor)
        {
            var result = new List<GeometryModel3D>();
            double w = PillarWidth;
            double t = 0.02;

            var mat = new MaterialGroup();
            mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(baseColor)));

            double xm = x - w, xp = x + w;
            double zm = -w, zp = w;

            // 4 vertical edges
            result.Add(CreateThinBox(xm - t, xm + t, top, bottom, zm - t, zm + t, mat));
            result.Add(CreateThinBox(xp - t, xp + t, top, bottom, zm - t, zm + t, mat));
            result.Add(CreateThinBox(xp - t, xp + t, top, bottom, zp - t, zp + t, mat));
            result.Add(CreateThinBox(xm - t, xm + t, top, bottom, zp - t, zp + t, mat));

            // 4 top edges
            result.Add(CreateThinBox(xm, xp, top - t, top + t, zm - t, zm + t, mat));
            result.Add(CreateThinBox(xm, xp, top - t, top + t, zp - t, zp + t, mat));
            result.Add(CreateThinBox(xm - t, xm + t, top - t, top + t, zm, zp, mat));
            result.Add(CreateThinBox(xp - t, xp + t, top - t, top + t, zm, zp, mat));

            // 4 bottom edges
            result.Add(CreateThinBox(xm, xp, bottom - t, bottom + t, zm - t, zm + t, mat));
            result.Add(CreateThinBox(xm, xp, bottom - t, bottom + t, zp - t, zp + t, mat));
            result.Add(CreateThinBox(xm - t, xm + t, bottom - t, bottom + t, zm, zp, mat));
            result.Add(CreateThinBox(xp - t, xp + t, bottom - t, bottom + t, zm, zp, mat));

            return result;
        }

        private GeometryModel3D CreateThinBox(double x0, double x1, double y0, double y1, double z0, double z1, Material material)
        {
            var mesh = new MeshGeometry3D();
            var p0 = new Point3D(x0, y1, z0);
            var p1 = new Point3D(x1, y1, z0);
            var p2 = new Point3D(x1, y1, z1);
            var p3 = new Point3D(x0, y1, z1);
            var p4 = new Point3D(x0, y0, z0);
            var p5 = new Point3D(x1, y0, z0);
            var p6 = new Point3D(x1, y0, z1);
            var p7 = new Point3D(x0, y0, z1);

            AddQuad(mesh, p0, p1, p5, p4);
            AddQuad(mesh, p2, p3, p7, p6);
            AddQuad(mesh, p3, p0, p4, p7);
            AddQuad(mesh, p1, p2, p6, p5);
            AddQuad(mesh, p0, p3, p2, p1);
            AddQuad(mesh, p4, p5, p6, p7);

            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        private GeometryModel3D CreateDisc(double x, double y, double radius, Color baseColor, byte alpha)
        {
            var mesh = new MeshGeometry3D();
            int segments = 24;

            for (int i = 0; i < segments; i++)
            {
                double a1 = 2 * Math.PI * i / segments;
                double a2 = 2 * Math.PI * (i + 1) / segments;

                int idx = mesh.Positions.Count;
                mesh.Positions.Add(new Point3D(x, y, 0));
                mesh.Positions.Add(new Point3D(x + radius * Math.Cos(a1), y, radius * Math.Sin(a1)));
                mesh.Positions.Add(new Point3D(x + radius * Math.Cos(a2), y, radius * Math.Sin(a2)));
                mesh.TriangleIndices.Add(idx);
                mesh.TriangleIndices.Add(idx + 1);
                mesh.TriangleIndices.Add(idx + 2);
                mesh.TriangleIndices.Add(idx);
                mesh.TriangleIndices.Add(idx + 2);
                mesh.TriangleIndices.Add(idx + 1);
            }

            var mat = new MaterialGroup();
            mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(
                Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B))));

            return new GeometryModel3D(mesh, mat) { BackMaterial = mat };
        }

        #endregion

        #region Arrows

        private GeometryModel3D CreateTubeArrow(Point3D start, Point3D end, Color color)
        {
            var mesh = CreateTubeMesh(start, end, TubeRadius, TubeSegments);

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 80));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(
                Color.FromArgb(120, color.R, color.G, color.B))));

            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        private GeometryModel3D CreateSlimTube(Point3D start, Point3D end, Color color)
        {
            var mesh = CreateTubeMesh(start, end, SelfCallTubeRadius, 8);
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(
                Color.FromArgb(140, color.R, color.G, color.B))));
            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        private GeometryModel3D CreateGlowTube(Point3D start, Point3D end, Color color)
        {
            var mesh = CreateTubeMesh(start, end, TubeRadius * 2.5, 6);

            var material = new MaterialGroup();
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(
                Color.FromArgb(15, color.R, color.G, color.B))));

            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        private MeshGeometry3D CreateTubeMesh(Point3D start, Point3D end, double radius, int segments)
        {
            var mesh = new MeshGeometry3D();
            var axis = end - start;
            if (axis.Length < 0.001) return mesh;

            var axisNorm = axis;
            axisNorm.Normalize();

            var perp1 = Vector3D.CrossProduct(axisNorm, new Vector3D(0, 0, 1));
            if (perp1.Length < 0.001)
                perp1 = Vector3D.CrossProduct(axisNorm, new Vector3D(1, 0, 0));
            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(axisNorm, perp1);
            perp2.Normalize();

            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                var offset = perp1 * (radius * Math.Cos(angle)) + perp2 * (radius * Math.Sin(angle));
                mesh.Positions.Add(start + offset);
            }
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                var offset = perp1 * (radius * Math.Cos(angle)) + perp2 * (radius * Math.Sin(angle));
                mesh.Positions.Add(end + offset);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.TriangleIndices.Add(i);
                mesh.TriangleIndices.Add(segments + i);
                mesh.TriangleIndices.Add(next);
                mesh.TriangleIndices.Add(next);
                mesh.TriangleIndices.Add(segments + i);
                mesh.TriangleIndices.Add(segments + next);
            }

            return mesh;
        }

        private GeometryModel3D CreateConeArrowhead(Point3D start, Point3D end, Color color)
        {
            var mesh = new MeshGeometry3D();
            var dir = end - start;
            if (dir.Length < 0.001)
                return new GeometryModel3D(mesh, new DiffuseMaterial(Brushes.Transparent));

            dir.Normalize();

            var perp1 = Vector3D.CrossProduct(dir, new Vector3D(0, 0, 1));
            if (perp1.Length < 0.001)
                perp1 = Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(dir, perp1);
            perp2.Normalize();

            var tip = end + dir * (ConeLength * 0.3);
            var baseCenter = end - dir * (ConeLength * 0.7);

            int segments = 12;
            mesh.Positions.Add(tip); // index 0

            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                var offset = perp1 * (ConeRadius * Math.Cos(angle)) + perp2 * (ConeRadius * Math.Sin(angle));
                mesh.Positions.Add(baseCenter + offset);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(1 + i);
                mesh.TriangleIndices.Add(1 + next);
            }

            int baseCenterIdx = mesh.Positions.Count;
            mesh.Positions.Add(baseCenter);
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.TriangleIndices.Add(baseCenterIdx);
                mesh.TriangleIndices.Add(1 + next);
                mesh.TriangleIndices.Add(1 + i);
            }

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(color)));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 80));

            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        private GeometryModel3D CreateSphere(Point3D center, double radius, Color color)
        {
            var mesh = new MeshGeometry3D();
            int stacks = 8, slices = 10;

            for (int stack = 0; stack <= stacks; stack++)
            {
                double phi = Math.PI * stack / stacks;
                double y = center.Y + radius * Math.Cos(phi);
                double r = radius * Math.Sin(phi);
                for (int slice = 0; slice <= slices; slice++)
                {
                    double theta = 2 * Math.PI * slice / slices;
                    mesh.Positions.Add(new Point3D(
                        center.X + r * Math.Cos(theta), y, center.Z + r * Math.Sin(theta)));
                }
            }

            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int i0 = stack * (slices + 1) + slice;
                    int i1 = i0 + 1;
                    int i2 = i0 + slices + 1;
                    int i3 = i2 + 1;
                    mesh.TriangleIndices.Add(i0);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i1);
                    mesh.TriangleIndices.Add(i1);
                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i3);
                }
            }

            var material = new MaterialGroup();
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(color)));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 100));

            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        #endregion

        #region Mesh Helpers

        private static void AddQuad(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            int i = mesh.Positions.Count;
            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);
            mesh.TriangleIndices.Add(i);
            mesh.TriangleIndices.Add(i + 1);
            mesh.TriangleIndices.Add(i + 2);
            mesh.TriangleIndices.Add(i);
            mesh.TriangleIndices.Add(i + 2);
            mesh.TriangleIndices.Add(i + 3);
        }

        #endregion

        /// <summary>Holds the visuals for one arrow step, for show/hide during playback.</summary>
        private class ArrowStepVisuals
        {
            public ModelVisual3D ModelVisual { get; set; }
            public Model3DGroup ModelGroup { get; set; }
            public List<BillboardTextVisual3D> Labels { get; set; }
        }
    }
}
