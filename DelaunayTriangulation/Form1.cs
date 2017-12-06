﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TriangleLib;

namespace Trianglex
{
    enum EditMode
    {
        Select,
        Add,
        Move
    }

    enum AddMode
    {
        None,
        Point,
        ConstrainedEdge
    }

    enum ViewMode
    {
        None,
        Zooming,
        Panning
    }

    enum TriangulationMode
    {
        Delaunay,
        ConformingDelaunay
    }

    struct DrawOptions
    {
        public bool DrawPSLG;
        public bool DrawFlippableEdges;
    }

    public partial class Form1 : Form
    {
        static readonly float POINT_SIZE = 10f;
        static readonly float HALF_POINT_SIZE = POINT_SIZE * 0.5f;
        static readonly float TOL = 10.0f;

        Random _rand;
        Timer _timer;

        List<Vec2> _points = new List<Vec2>();
        PSLG _pslg = new PSLG();

        ConformingDelaunayTriangulation _conformingTriangulation;
        DelaunayTriangulation _delaunayTriangulation;

        float _zoom = 1.0f;
        float _zoomInverse = 1.0f;
        Vec2 _origin = new Vec2();
        Point _lastMousePosition;

        int _movingPointIndex = 1;
        List<int> _selectedIndices = new List<int>();
        Vec2 _v0;
        Edge _tempEdge;

        DrawOptions _drawOptions = new DrawOptions();
        EditMode _editMode = EditMode.Select;
        AddMode _addMode = AddMode.None;
        ViewMode _viewMode = ViewMode.None;
        TriangulationMode _triangulationMode = TriangulationMode.Delaunay;

        public Form1()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            tsbMode.Tag = EditMode.Select;
            tsbAddMode.Tag = AddMode.Point;
            _addMode = AddMode.Point;
            _editMode = EditMode.Select;

            _timer = new Timer();
            _timer.Tick += (sender, e) =>
            {
                //this.UpdatePoints();
                this.Invalidate();
            };

            _rand = new Random((int)DateTime.Now.Ticks);
        }

        private List<Vec2> FillPoints(int pointCount, RectangleF bounds)
        {
            var points = new List<Vec2>();

            for (int i = 0; i < pointCount; i++)
            {
                var x = (_rand.NextDouble() * 2.0 - 1.0) * bounds.Width * 0.5;
                var y = (_rand.NextDouble() * 2.0 - 1.0) * bounds.Height * 0.5;
                points.Add(new Vec2() { X = Math.Round(x), Y = Math.Round(y) });
            }

            return points;
        }

        protected override void OnShown(EventArgs e)
        {
            UpdatePoints();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    HandleLeftMouseDown(e);
                    break;
                case MouseButtons.Right:
                    break;
                case MouseButtons.Middle:
                    _viewMode = ViewMode.Panning;
                    _lastMousePosition = e.Location;
                    break;
                default:
                    break;
            }
        }

        private void HandleLeftMouseDown(MouseEventArgs e)
        {
            var p = SelectPoint(e.Location);

            switch (_editMode)
            {
                case EditMode.Select:
                    {
                        if (p != null)
                        {
                            if (Form.ModifierKeys == Keys.Control)
                                _selectedIndices.Add(_points.IndexOf(p));
                            else
                            {
                                _selectedIndices.Clear();
                                _selectedIndices.Add(_points.IndexOf(p));
                            }
                        }

                        break;
                    }
                case EditMode.Move:
                    if (p != null)
                        _movingPointIndex = _points.IndexOf(p);

                    break;
                case EditMode.Add:
                    {
                        if (_pslg == null)
                            _pslg = new PSLG();

                        if (_v0 == null)
                            _v0 = p ?? PointToWorld(e.Location);

                        break;
                    }
                default:
                    break;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_viewMode == ViewMode.Panning)
            {
                _origin += new Vec2(
                    _lastMousePosition.X - e.Location.X,
                    -(_lastMousePosition.Y - e.Location.Y));
            }
            else if (_editMode == EditMode.Move)
            {
                if (_movingPointIndex == -1)
                    return;

                
                if (_triangulationMode == TriangulationMode.Delaunay)
                {
                    var p0 = PointToWorld(e.Location);
                    var p1 = _points[_movingPointIndex];

                    var dist = Math.Abs(Vec2.Length(p0 - p1));

                    _points[_movingPointIndex] = new Vec2(Math.Round(p0.X), Math.Round(p0.Y));

                    if (_points.Count > 2)
                        _delaunayTriangulation = DelaunayTriangulation.Triangulate(_points);
                }
                else if (_triangulationMode == TriangulationMode.ConformingDelaunay)
                {
                    _conformingTriangulation = ConformingDelaunayTriangulation.Triangulate(_pslg, _points, TOL);
                    _pslg = _conformingTriangulation.Pslg;
                    _points = _pslg.Vertices.Select(p => p.Position).ToList();
                }
            }
            else if (_addMode == AddMode.ConstrainedEdge)
            {
                if (_v0 != null)
                {
                    var p = SelectPoint(e.Location) ?? PointToWorld(e.Location);

                    if (_v0 != p)
                        _tempEdge = new Edge(new Vertex(_v0), new Vertex(p));
                }
            }

            _lastMousePosition = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_addMode == AddMode.Point)
                {
                    var p = PointToWorld(e.Location);

                    _points.Add(new Vec2(Math.Round(p.X), Math.Round(p.Y)));
                }
                else if (_addMode == AddMode.ConstrainedEdge)
                {
                    if (_tempEdge != null)
                        _pslg.AddEdge(_tempEdge);
                }

                if (_triangulationMode == TriangulationMode.Delaunay)
                {
                    if (_points.Count > 2)
                        _delaunayTriangulation = DelaunayTriangulation.Triangulate(_points);
                }
                else if (_triangulationMode == TriangulationMode.ConformingDelaunay)
                {
                    _conformingTriangulation = ConformingDelaunayTriangulation.Triangulate(_pslg, _points, TOL);
                    _pslg = _conformingTriangulation.Pslg;
                    _points = _pslg.Vertices.Select(p => p.Position).ToList();
                }
            }

            _viewMode = ViewMode.None;

            _movingPointIndex = -1;

            _v0 = null;
            _tempEdge = null;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            var halfWidth = ClientRectangle.Width * 0.5f;
            var halfHeight = ClientRectangle.Height * 0.5f;

            var p = new Vec2(e.Location.X - halfWidth, -(e.Location.Y - halfHeight));
            p += _origin;

            if (e.Delta > 0)
            {
                _zoom *= 2.0f;
                _zoomInverse = 1.0f / _zoom;
                _origin += p;
            }
            else
            {
                p *= _zoomInverse;
                _zoom *= 0.5f;
                _zoomInverse = 1.0f / _zoom;
                _origin -= (p * _zoom);
            }
        }

        private Vec2 PointToWorld(Point screenCoords)
        {
            var halfWidth = ClientRectangle.Width * 0.5f;
            var halfHeight = ClientRectangle.Height * 0.5f;
            var p = new Vec2(screenCoords.X - halfWidth, -(screenCoords.Y - halfHeight));

            p += _origin;
            p *= _zoomInverse;
            return p;
        }

        private Vec2 SelectPoint(Point screenCoords)
        {
            var position = PointToWorld(screenCoords);

            var minDist = 5 * _zoomInverse;
            for (int i = 0; i < _points.Count; i++)
            {
                var point = _points[i];

                if (Math.Abs(position.X - point.X) < minDist && Math.Abs(position.Y - point.Y) < minDist)
                    return _points[i];
            }

            return null;
        }

        private void UpdatePoints()
        {
            _timer.Stop();

            // rand

            //var halfWidth = ClientRectangle.Width * 0.5f * _zoom;
            //var halfHeight = ClientRectangle.Height * 0.5f * _zoom;

            //var bounds = new RectangleF(
            //    new PointF(-halfWidth, -halfHeight),
            //    new SizeF(ClientRectangle.Width, ClientRectangle.Height));

            //for (int i = 0; i < 1; i++)
            //{
            //    _points = FillPoints(_totalPoints, bounds);
            //    _triangles = DelaunayTriangulation.Triangulate(_points);
            //}



            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(2488.81787109375, 1113.5077583591)), new Vertex(new Vec2(2488.81787109375, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(2363.49889793366, 1115.24652929936)), new Vertex(new Vec2(2363.76858208171, 1111.54013426594))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1969.04443359375, 1105.32923334156)), new Vertex(new Vec2(1969.04443359375, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1869.42443847656, 1115.24652929936)), new Vertex(new Vec2(1869.42443847656, 1103.76173380517))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1635.11669921875, 1100.07495112435)), new Vertex(new Vec2(1635.11669921875, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1535.49658203125, 1115.24652929936)), new Vertex(new Vec2(1535.49658203125, 1098.5074496672))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1344.23388671875, 1095.49797164144)), new Vertex(new Vec2(1344.23388671875, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1245.75390625, 1115.24652929936)), new Vertex(new Vec2(1245.75390625, 1093.94840999422))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1117.72436523438, 1094.59108010523)), new Vertex(new Vec2(1117.72436523438, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(1117.72436523438, 1091.93389226662)), new Vertex(new Vec2(1117.72436523438, 1094.59108010523))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(983.264404296875, 1115.24652929936)), new Vertex(new Vec2(983.264404296875, 1089.81819324269))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(837.556284440744, 1087.52550681909)), new Vertex(new Vec2(835.539721799644, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(344.760589346842, 1103.86763674669)), new Vertex(new Vec2(341.220656622349, 1079.71577077577))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(346.428390673836, 1115.24652929936)), new Vertex(new Vec2(344.760589346842, 1103.86763674669))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(-1149.67114257813, 1056.25690389552)), new Vertex(new Vec2(2599.32275390625, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(2599.32275390625, 1115.24652929936)), new Vertex(new Vec2(-1149.67114257813, 1115.24652929936))));
            //_pslg.AddEdge(new Edge(new Vertex(new Vec2(-1149.67114257813, 1115.24652929936)), new Vertex(new Vec2(-1149.67114257813, 1056.25690389552))));


            //foreach (var edge in _pslg.Edges)
            //{
            //    if (!_points.Contains(Vec2.Round(edge.V0.Position)))
            //        _points.Add(Vec2.Round(edge.V0.Position));

            //    if (!_points.Contains(Vec2.Round(edge.V1.Position)))
            //        _points.Add(Vec2.Round(edge.V1.Position));
            //}

            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_points == null)
                return;

            var g = e.Graphics;
            var halfWidth = ClientRectangle.Width * 0.5f;
            var halfHeight = ClientRectangle.Height * 0.5f;

            InitDrawing(g, halfWidth, halfHeight);
            DrawAxis(g, halfWidth, halfHeight);

            DrawTriangulation(g);

            DrawPoints(g, _points, Color.Blue);
            var selected = _selectedIndices.Select(p => _points.ElementAt(p)).ToList();

            if (selected.Count > 0)
                DrawPoints(g, selected, Color.Red);

            if (_drawOptions.DrawPSLG)
                DrawPSLG(g);

            if (_addMode == AddMode.ConstrainedEdge && _tempEdge != null)
            {
                using (var pen = new Pen(new SolidBrush(Color.Gray), -1.0f))
                {
                    pen.DashStyle = DashStyle.DashDot;
                    Draw(g, _tempEdge, pen);
                }
            }
        }

        private void DrawPSLG(Graphics g)
        {
            if (_pslg != null)
            {
                foreach (var edge in _pslg.Edges)
                    Draw(g, edge, Color.Maroon, 2.0f * _zoomInverse);
            }
        }

        private void DrawTriangulation(Graphics g)
        {
            if (_conformingTriangulation != null)
                DrawConformingTriangulation(g, _conformingTriangulation);
            else if (_delaunayTriangulation != null)
                DrawDelaunayTriangulation(g, _delaunayTriangulation);
        }

        private void DrawDelaunayTriangulation(Graphics g, DelaunayTriangulation triangulation)
        {
            var triangles = triangulation.Triangles;

            if (triangles != null)
                DrawTriangles(g, triangles);
        }

        private void DrawConformingTriangulation(Graphics g, ConformingDelaunayTriangulation triangulation)
        {
            var triangles = triangulation.Triangles;

            if (triangles != null)
                DrawTriangles(g, triangles);
        }

        private void InitDrawing(Graphics g, float halfWidth, float halfHeight)
        {
            var clientToWorld = new Matrix(
                            1.0f, 0.0f,
                            0.0f, -1.0f,
                            halfWidth, halfHeight);

            g.MultiplyTransform(clientToWorld);
            g.TranslateTransform(-(float)_origin.X, -(float)_origin.Y);
            g.ScaleTransform(_zoom, _zoom);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.Clear(Color.White);
        }

        private void DrawAxis(Graphics g, float halfWidth, float halfHeight)
        {
            var w0 = (-halfWidth + (float)_origin.X) * (_zoomInverse);
            var w1 = (halfWidth+ (float)_origin.X) * (_zoomInverse);
            var h0 = (-halfHeight + (float)_origin.Y) * (_zoomInverse);
            var h1 = (halfHeight + (float)_origin.Y) * (_zoomInverse);

            g.SmoothingMode = SmoothingMode.Default;
            using (var pen = new Pen(Brushes.Black, -1))
            {
                g.DrawLine(pen, new PointF(w0, 0.0f), new PointF(w1, 0.0f));
                g.DrawLine(pen, new PointF(0.0f, h0), new PointF(0.0f, h1));
            }

            var gridSize = 10.0f;

            using (var pen = new Pen(Brushes.Gray, -1))
            {
                pen.DashStyle = DashStyle.Dot;
                var x = 0.0f;
                do
                {
                    x += gridSize;
                    g.DrawLine(pen, new PointF(x, h0), new PointF(x, h1));
                } while (x < w1);

                x = 0.0f;
                do 
                {
                    x -= gridSize;
                    g.DrawLine(pen, new PointF(x, h0), new PointF(x, h1));
                } while (x > w0);

                var y = 0.0f;
                do
                {
                    y += gridSize;
                    g.DrawLine(pen, new PointF(w0, y), new PointF(w1, y));
                } while (y < h1);

                y = 0.0f;
                do
                {
                    y -= gridSize;
                    g.DrawLine(pen, new PointF(w0, y), new PointF(w1, y));
                } while (y > h0);
            }

            g.SmoothingMode = SmoothingMode.HighQuality;
        }

        private void DrawPoints(Graphics g, List<Vec2> points, Color color)
        {
            foreach (var point in points)
            {
                using (var brush = new SolidBrush(color))
                    Draw(g, point, brush);

                var rect = new RectangleF((float)point.X - TOL, (float)point.Y - TOL, 2.0f * TOL, 2.0f * TOL);

                using (var pen = new Pen(color, -1))
                    g.DrawEllipse(pen, rect);
            }
        }

        private void DrawTriangles(Graphics g, List<Triangle> triangles)
        {
            var p = PointToWorld(_lastMousePosition);

            foreach (var triangle in triangles)
                Draw(g, triangle, Color.Green);
        }

        private void DrawCircumcircle(Graphics g, Triangle triangle, Pen pen)
        {
            double x;
            double y;
            double d;
            PointF p;

            if (triangle.V2 != null)
            {
                var A = triangle.V0.Position;
                var B = triangle.V1.Position;
                var C = triangle.V2.Position;

                var a = A - C;
                var b = B - C;

                var r = Vec2.Length(A - B) / (2.0 * Vec2.Cross(Vec2.Normalize(a), Vec2.Normalize(b)));

                var sla = Vec2.SquaredLength(a);
                var slb = Vec2.SquaredLength(b);
                var aa = (sla * b - slb * a);
                var bb = a;
                var cc = b;
                var dd = ((Vec2.Dot(aa, cc) * bb) - (Vec2.Dot(aa, bb) * cc)) / (2.0 * (sla * slb - Math.Pow(Vec2.Dot(a, b), 2.0)));

                p = (dd + C).ToPointF();
                x = p.X - r;
                y = p.Y - r;
                d = r * 2.0;
            }
            else
            {
                p = ((triangle.V0.Position + triangle.V1.Position) / 2.0).ToPointF();
                d = Vec2.Length(triangle.V0.Position - triangle.V1.Position);
                x = p.X - d * 0.5;
                y = p.Y - d * 0.5;
            }

            g.DrawEllipse(pen, (float)x, (float)y, (float)d, (float)d);
            g.FillEllipse(Brushes.Black, (float)p.X - 2.0f, (float)p.Y - 2.0f, (float)4.0f, (float)4.0f);
        }

        private void Draw(Graphics g, Vec2 point, Brush brush)
        {
            var x = HALF_POINT_SIZE * _zoomInverse;
            var y = HALF_POINT_SIZE * _zoomInverse;
            var w = POINT_SIZE * _zoomInverse;
            var h = POINT_SIZE * _zoomInverse;

            var rect = new RectangleF(
                (float)point.X - x,
                (float)point.Y - y,
                w,
                h);

            g.FillEllipse(brush, rect);
            var state = g.Save();

            var halfWidth = ClientRectangle.Width * 0.5f;
            var halfHeight = ClientRectangle.Height * 0.5f;

            var clientToWorld = new Matrix(
                            1.0f, 0.0f,
                            0.0f, -1.0f,
                            0, 0);

            g.MultiplyTransform(clientToWorld);

            using (var font = new Font(this.Font.FontFamily, 8.0f * _zoomInverse))
                g.DrawString(string.Format("({0}, {1})", point.X, point.Y), font, Brushes.Black, new PointF(rect.X + 6 * _zoomInverse, -rect.Y - 10 * _zoomInverse));

            g.Restore(state);
        }

        private void Draw(Graphics g, Triangle triangle, Color color)
        {
            if (triangle.E0 != null)
                Draw(g, triangle.E0, _drawOptions.DrawFlippableEdges && !triangle.E0.CanFlip() ? Color.Red : color);

            if (triangle.E1 != null)
                Draw(g, triangle.E1, _drawOptions.DrawFlippableEdges && !triangle.E1.CanFlip() ? Color.Red : color);

            if (triangle.E2 != null)
                Draw(g, triangle.E2, _drawOptions.DrawFlippableEdges && !triangle.E2.CanFlip() ? Color.Red : color);
        }

        private void Draw(Graphics g, Edge edge, Pen pen)
        {
            var v0 = edge.V0.Position.ToPointF();
            var v1 = edge.V1.Position.ToPointF();
            g.DrawLine(pen, v0.X, v0.Y, v1.X, v1.Y);
        }

        private void Draw(Graphics g, Edge edge, Color color, float width = -1.0f)
        {
            var pen = new Pen(new SolidBrush(color), width);
            Draw(g, edge, pen);
        }

        private void tsbMode_ButtonClick(object sender, EventArgs e)
        {
            if (tsbMode.Tag == null)
                tsbMode.Tag = EditMode.Select;
            
            switch ((EditMode)tsbMode.Tag)
            {
                case EditMode.Select:
                    ChangeToSelectMode();
                    break;
                case EditMode.Move:
                    ChangeToMoveMode();
                    break;
                default:
                    break;
            }
        }

        private void tsmSelect_Click(object sender, EventArgs e)
        {
            ChangeToSelectMode();
        }

        private void tsmMove_Click(object sender, EventArgs e)
        {
            ChangeToMoveMode();
        }

        private void tsmClearPoints_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            _points.Clear();
            _delaunayTriangulation = null;
            _conformingTriangulation = null;
            _pslg = null;
            _timer.Start();
        }

        private void tsmAddPoints_Click(object sender, EventArgs e)
        {
            tsbMode.Tag = _editMode;
            _editMode = EditMode.Add;
            _addMode = AddMode.Point;
            tsbAddMode.Image = tsmAddPoints.Image;

        }

        private void tsmConstrainedEdge_Click(object sender, EventArgs e)
        {
            tsbMode.Tag = _editMode;
            _editMode = EditMode.Add;
            _addMode = AddMode.ConstrainedEdge;
            _drawOptions.DrawPSLG = true;
            _triangulationMode = TriangulationMode.ConformingDelaunay;
            tsmShowPSLG.Checked = true;
            tsbAddMode.Image = tsmConstrainedEdge.Image;
        }

        private void ChangeToSelectMode()
        {
            _editMode = EditMode.Select;
            _selectedIndices.Clear();
            _addMode = AddMode.None;

            tsbMode.Image = tsmSelect.Image;
            tsmAddPoints.Checked = false;
            tsmConstrainedEdge.Checked = false;
        }

        private void ChangeToMoveMode()
        {
            _editMode = EditMode.Move;
            tsbMode.Image = tsmMove.Image;
            _addMode = AddMode.None;

            tsmAddPoints.Checked = false;
            tsmConstrainedEdge.Checked = false;
        }

        private void tsmShowPSLG_Click(object sender, EventArgs e)
        {
            _drawOptions.DrawPSLG = !_drawOptions.DrawPSLG;
        }

        private void tsmShowFlippableEdges_Click(object sender, EventArgs e)
        {
            _drawOptions.DrawFlippableEdges = !_drawOptions.DrawFlippableEdges;
        }

        private void tsmDelaunay_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            _triangulationMode = TriangulationMode.Delaunay;
            _delaunayTriangulation = DelaunayTriangulation.Triangulate(_points);
            _timer.Start();
        }

        private void tsmConformingDelaunay_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            _triangulationMode = TriangulationMode.ConformingDelaunay;
            _conformingTriangulation = ConformingDelaunayTriangulation.Triangulate(_pslg, _points, TOL);
            _pslg = _conformingTriangulation.Pslg;
            _points = _pslg.Vertices.Select(p => p.Position).ToList();
            _timer.Start();
        }
    }

    public static class Vec2Extension
    {
        public static PointF ToPointF(this Vec2 p)
        {
            return new PointF((float)p.X, (float)p.Y);
        }
    }
}

