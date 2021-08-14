﻿using System;
using System.Drawing;
using System.Linq;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Prism.Extensions;
using Prism.Render.Shader;
using Prism.Resources;
using RainbowForge;
using RainbowForge.Mesh;

namespace Prism.Render
{
	public class ModelRenderer
	{
		private readonly IRenderContext _renderContext;
		private readonly VertexBuffer _vbo = new();

		private Camera _camera;

		private BoundingBox[] _modelBb = Array.Empty<BoundingBox>();
		private Vector2 _prevMousePoint = Vector2.Zero;
		private Vector2 _rotation = new(-35.264f, 45);

		private int _screenVao = -1;
		private ShaderProgram _shaderModel;
		private ShaderProgram _shaderScreen;

		private bool _hasTexture;
		private int _textureId;
		private Vector2 _translation = new(0, 0);
		private Framebuffer _viewFbo;

		private int _zoom = 1;

		private Mesh _mesh;

		public ModelRenderer(IRenderContext renderContext)
		{
			_renderContext = renderContext;

			_camera = new Camera
			{
				Position = new Vector3(10, 10, 10),
				Rotation = new Vector2(35.264f, -45)
			};
		}

		private void CreateScreenVao()
		{
			float[] quadVertices =
			{
				// positions   // texCoords
				-1.0f, 1.0f, 0.0f, 1.0f,
				-1.0f, -1.0f, 0.0f, 0.0f,
				1.0f, -1.0f, 1.0f, 0.0f,

				-1.0f, 1.0f, 0.0f, 1.0f,
				1.0f, -1.0f, 1.0f, 0.0f,
				1.0f, 1.0f, 1.0f, 1.0f
			};

			_screenVao = GL.GenVertexArray();
			var screenVbo = GL.GenBuffer();
			GL.BindVertexArray(_screenVao);
			GL.BindBuffer(BufferTarget.ArrayBuffer, screenVbo);
			GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices,
				BufferUsageHint.StaticDraw);
			GL.EnableVertexAttribArray(0);
			GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
			GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices,
				BufferUsageHint.StaticDraw);
			GL.EnableVertexAttribArray(1);
			GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
			GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			GL.BindVertexArray(0);
		}

		private void DrawFullscreenQuad()
		{
			GL.Disable(EnableCap.DepthTest);
			GL.Enable(EnableCap.Texture2D);

			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2DMultisample, _viewFbo.Texture);

			GL.BindVertexArray(_screenVao);
			GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

			GL.Disable(EnableCap.Texture2D);
			GL.Enable(EnableCap.DepthTest);
		}

		public void Render()
		{
			var width = _renderContext.Width;
			var height = _renderContext.Height;

			if (_screenVao == -1)
			{
				CreateScreenVao();
				_viewFbo = new Framebuffer(8, _renderContext.DefaultFramebuffer);

				_shaderScreen = new ShaderProgram(ResourceHelper.ReadResource("screen.frag"), ResourceHelper.ReadResource("screen.vert"));
				_shaderScreen.Uniforms.SetValue("texScene", 0);
				_shaderScreen.Uniforms.SetValue("samplesScene", _viewFbo.Samples);

				_shaderModel = new ShaderProgram(ResourceHelper.ReadResource("model.frag"), ResourceHelper.ReadResource("model.vert"));
				_shaderModel.Uniforms.SetValue("texModel", 1);
				_shaderModel.Uniforms.SetValue("lightPos", new Vector3(0.6f, -1, 0.8f));

				_textureId = GL.GenTexture();
			}

			if (width != _viewFbo.Width || height != _viewFbo.Height)
			{
				_viewFbo.Init(width, height);
				_shaderScreen.Uniforms.SetValue("width", width);
				_shaderScreen.Uniforms.SetValue("height", height);
			}

			var aspectRatio = width / (float)height;
			var perspective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver2, aspectRatio, 1f, 65536);

			GL.Viewport(0, 0, width, height);

			GL.ClearColor(Color.Black);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			var rotation = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-_rotation.Y))
			               * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-_rotation.X));

			var view = rotation
			           * Matrix4.CreateScale((float)Math.Pow(10, _zoom / 10f) * Vector3.One)
			           * Matrix4.CreateTranslation(_translation.X, _translation.Y, -10);

			var modelspace = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90));

			var model = modelspace;

			_shaderModel.Uniforms.SetValue("m", model);
			_shaderModel.Uniforms.SetValue("v", view);
			_shaderModel.Uniforms.SetValue("p", perspective);

			GL.Enable(EnableCap.DepthTest);
			GL.Disable(EnableCap.Texture2D);

			_viewFbo.Use();
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			// 3D Viewport
			{
				GL.PushMatrix();

				GL.MatrixMode(MatrixMode.Projection);
				GL.LoadMatrix(ref perspective);
				GL.MatrixMode(MatrixMode.Modelview);
				GL.LoadMatrix(ref view);

				RenderOriginAxes();

				GL.PopMatrix();

				// GL.MultMatrix(ref model);
				//
				// RenderBoundingBox();

				GL.Color4(Color.White);

				GL.ActiveTexture(TextureUnit.Texture1);
				GL.BindTexture(TextureTarget.Texture2D, _hasTexture ? _textureId : 0);

				_shaderModel.Use();
				_vbo.Render(PrimitiveType.Triangles);
				_shaderModel.Release();

				GL.Disable(EnableCap.Texture2D);
			}

			// 2D Viewport
			{
				GL.PushMatrix();

				GL.MatrixMode(MatrixMode.Projection);
				GL.LoadIdentity();
				GL.Ortho(0, width, height, 0, -100, 100);
				GL.MatrixMode(MatrixMode.Modelview);
				GL.LoadIdentity();

				// Gizmo
				GL.PushMatrix();
				GL.Translate(40, height - 40, 0);

				GL.Scale(30 * Vector3.One);
				GL.Scale(1, -1, 1);

				GL.MultMatrix(ref rotation);

				RenderOriginAxes();
				GL.PopMatrix();

				// UV map
				// GL.PushMatrix();
				// GL.Translate(width - 310, height - 310, 0);
				//
				// GL.Scale(300, 300, 1);
				//
				// GL.Color4(Color.Gray);
				// GL.Begin(PrimitiveType.Lines);
				//
				// GL.Vertex2(0, 0);
				// GL.Vertex2(1, 0);
				//
				// GL.Vertex2(1, 0);
				// GL.Vertex2(1, 1);
				//
				// GL.Vertex2(1, 1);
				// GL.Vertex2(0, 1);
				//
				// GL.Vertex2(0, 1);
				// GL.Vertex2(0, 0);
				//
				// GL.End();
				//
				// GL.Color4(Color.White);
				//
				// if (_mesh != null)
				// 	foreach (var o in _mesh.Objects.Take(1))
				// 	{
				// 		foreach (var pointer in o)
				// 		{
				// 			var a = _mesh.Container.TexCoords[pointer.A];
				// 			var b = _mesh.Container.TexCoords[pointer.B];
				// 			var c = _mesh.Container.TexCoords[pointer.C];
				//
				// 			GL.Begin(PrimitiveType.LineLoop);
				// 			GL.Vertex2(a.X, a.Y);
				// 			GL.Vertex2(b.X, b.Y);
				// 			GL.Vertex2(c.X, c.Y);
				// 			GL.End();
				// 		}
				// 	}
				//
				// GL.PopMatrix();

				GL.PopMatrix();
			}
			_viewFbo.Release();

			{
				GL.PushMatrix();

				GL.MatrixMode(MatrixMode.Projection);
				GL.LoadIdentity();
				GL.Ortho(-1, 1, -1, 1, -1, 1);
				GL.MatrixMode(MatrixMode.Modelview);
				GL.LoadIdentity();

				_shaderScreen.Use();
				DrawFullscreenQuad();
				_shaderScreen.Release();

				GL.PopMatrix();
			}
		}

		private static void RenderOriginAxes()
		{
			GL.Color4(Color.Red);
			GL.Begin(PrimitiveType.LineStrip);
			GL.Vertex3(0, 0, 0);
			GL.Vertex3(1, 0, 0);
			GL.End();
			GL.Color4(Color.LawnGreen);
			GL.Begin(PrimitiveType.LineStrip);
			GL.Vertex3(0, 0, 0);
			GL.Vertex3(0, 1, 0);
			GL.End();
			GL.Color4(Color.Blue);
			GL.Begin(PrimitiveType.LineStrip);
			GL.Vertex3(0, 0, 0);
			GL.Vertex3(0, 0, 1);
			GL.End();
		}

		private void RenderBoundingBox()
		{
			GL.Color4(Color.White);
			GL.Begin(PrimitiveType.Lines);

			foreach (var b in _modelBb)
			{
				var max = new Vector3(b.MaxX, b.MaxY, b.MaxZ);
				var min = new Vector3(b.MinX, b.MinY, b.MinZ);

				// X
				GL.Vertex3(min.X, min.Y, min.Z);
				GL.Vertex3(max.X, min.Y, min.Z);

				GL.Vertex3(min.X, max.Y, min.Z);
				GL.Vertex3(max.X, max.Y, min.Z);

				GL.Vertex3(min.X, max.Y, max.Z);
				GL.Vertex3(max.X, max.Y, max.Z);

				GL.Vertex3(min.X, min.Y, max.Z);
				GL.Vertex3(max.X, min.Y, max.Z);

				GL.Vertex3(min.X, min.Y, max.Z);
				GL.Vertex3(max.X, min.Y, max.Z);

				// Y
				GL.Vertex3(min.X, min.Y, min.Z);
				GL.Vertex3(min.X, max.Y, min.Z);

				GL.Vertex3(max.X, min.Y, min.Z);
				GL.Vertex3(max.X, max.Y, min.Z);

				GL.Vertex3(max.X, min.Y, max.Z);
				GL.Vertex3(max.X, max.Y, max.Z);

				GL.Vertex3(min.X, min.Y, max.Z);
				GL.Vertex3(min.X, max.Y, max.Z);

				// Z
				GL.Vertex3(min.X, min.Y, min.Z);
				GL.Vertex3(min.X, min.Y, max.Z);

				GL.Vertex3(min.X, max.Y, min.Z);
				GL.Vertex3(min.X, max.Y, max.Z);

				GL.Vertex3(max.X, max.Y, min.Z);
				GL.Vertex3(max.X, max.Y, max.Z);

				GL.Vertex3(max.X, min.Y, min.Z);
				GL.Vertex3(max.X, min.Y, max.Z);
			}

			GL.End();
		}

		public void OnMouseMove(Point devPt, bool leftMouse, bool rightMouse)
		{
			var mousePoint = new Vector2(devPt.X, devPt.Y);

			if (_prevMousePoint != Vector2.Zero)
			{
				if (rightMouse)
				{
					_translation.X += (mousePoint.X - _prevMousePoint.X) / 50f;
					_translation.Y -= (mousePoint.Y - _prevMousePoint.Y) / 50f;
				}
				else if (leftMouse)
				{
					_rotation.X -= (mousePoint.Y - _prevMousePoint.Y) / 2f;
					_rotation.Y -= (mousePoint.X - _prevMousePoint.X) / 2f;
				}
			}

			_prevMousePoint = mousePoint;

			_renderContext.MarkDirty();
		}

		public void OnMouseDown(Point devPt)
		{
			_prevMousePoint = new Vector2(devPt.X, devPt.Y);

			_renderContext.MarkDirty();
		}

		public void OnMouseWheel(float delta)
		{
			const int d = 1;
			var m1 = 1; //keyboard[Key.LShift] ? 10 : 1;
			var m2 = 1; //_keyboard[Key.LControl] ? 100 : 1;

			var deltaZoom = Math.Sign(delta) * d * m1 * m2;

			_zoom += deltaZoom;

			if (_zoom > 350)
				_zoom = 350;

			if (_zoom < -350)
				_zoom = -350;

			_renderContext.MarkDirty();
		}

		public void BuildModelQuads(Mesh mesh)
		{
			if (mesh.Objects.Count == 0)
				return;

			_mesh = mesh;

			_vbo.InitializeVbo(
				mesh.Container.Vertices,
				mesh.Container.Normals,
				mesh.Container.TexCoords,
				mesh.Objects.Take((int)(mesh.Objects.Count / mesh.MeshHeader.NumLods)).SelectMany(pointers => pointers.SelectMany(pointer => new uint[] { pointer.A, pointer.B, pointer.C })).ToArray()
			);

			_renderContext.MarkDirty();
		}

		public void SetPartBounds(BoundingBox[] bounds)
		{
			_modelBb = bounds;

			_renderContext.MarkDirty();
		}

		public void SetTexture(Bitmap bmp)
		{
			_hasTexture = bmp != null;

			if (bmp == null)
				return;

			bmp.LoadGlTexture(_textureId);

			_renderContext.MarkDirty();
		}

		public void ResetView()
		{
			_zoom = 1;
			_rotation = new Vector2(-35.264f, 45);
			_translation = Vector2.Zero;

			_renderContext.MarkDirty();
		}
	}
}