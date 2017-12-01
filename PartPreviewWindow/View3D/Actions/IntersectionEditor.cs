﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class IntersectionEditor : IObject3DEditor
	{
		private MeshWrapperOperation group;
		private View3DWidget view3DWidget;
		public string Name => "Intersection";

		public bool Unlocked { get; } = true;
		bool processed = false;

		public GuiWidget Create(IObject3D group, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.group = group as MeshWrapperOperation;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			if (group is MeshWrapperOperation
				&& !processed)
			{
				bool first = true;
				// set all but one mesh to look like holes
				foreach (var item in group.Descendants().Where((obj) => obj.OwnerID == group.ID).ToList())
				{
					item.OutputType = first ? PrintOutputTypes.Solid : PrintOutputTypes.Hole;
					first = false;
				}

				ProcessBooleans(group);
			}

			return mainContainer;
		}

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(MeshWrapperOperation),
		};

		private async void ProcessBooleans(IObject3D group)
		{
			processed = true;
			{
				// spin up a task to remove holes from the objects in the group
				await Task.Run(() =>
				{
					var participants = group.Descendants().Where((obj) => obj.OwnerID == group.ID);

					if (participants.Count() > 1)
					{
						var first = participants.First();

						foreach (var remove in participants)
						{
							if(remove != first)
							{
								var transformedRemove = Mesh.Copy(remove.Mesh, CancellationToken.None);
								transformedRemove.Transform(remove.WorldMatrix());

								var transformedKeep = Mesh.Copy(first.Mesh, CancellationToken.None);
								transformedKeep.Transform(first.WorldMatrix());

								transformedKeep = PolygonMesh.Csg.CsgOperations.Intersect(transformedKeep, transformedRemove);
								var inverse = first.WorldMatrix();
								inverse.Invert();
								transformedKeep.Transform(inverse);
								first.Mesh = transformedKeep;
								remove.Visible = false;
							}
						}
					}
				});
			}
		}
	}
}