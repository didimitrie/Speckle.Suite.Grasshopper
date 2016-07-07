using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpeckleSuite
{
    public class StreamConverter
    {

        public static dynamic castFromGH(object obj, string groupName = "default", string groupId = "0")
        {
            dynamic returnObject = new System.Dynamic.ExpandoObject();
            returnObject.groupName = groupName;

            GH_Point myPoint = obj as GH_Point;
            if (myPoint != null)
            {
                returnObject.type = "GH_Point";
                returnObject.value = myPoint.Value;
            }

            GH_Vector myVector = obj as GH_Vector;
            if (myVector != null)
            {
                returnObject.type = "GH_Vector";
                returnObject.value = myVector.Value;
            }

            GH_Plane myPlane = obj as GH_Plane;
            if (myPlane != null)
            {
                returnObject.type = "GH_Plane";
                returnObject.value = getPlane(myPlane.Value);
            }

            GH_Curve myCurve = obj as GH_Curve;
            if (myCurve != null)
            {
                returnObject.type = "GH_Curve";
                PolylineCurve myPolyline = myCurve.Value as PolylineCurve;
                if (myPolyline != null)
                {
                    returnObject.type = "GH_Polyline";
                    returnObject.value = getPolyline(myPolyline);
                }
                else
                {
                    returnObject.value = ObjectToString(myCurve.Value);
                    PolylineCurve p = myCurve.Value.ToPolyline(0, 1, 0, 0, 0, 0.1, 0, 0, true);
                    returnObject.displayValue = getPolyline(p);
                }
            }

            GH_Arc myArc = obj as GH_Arc;
            if (myArc != null)
            {
                returnObject.type = "GH_Arc";
                returnObject.value = ObjectToString(myArc.Value);
            }

            GH_Circle myCircle = obj as GH_Circle;
            if (myCircle != null)
            {
                returnObject.type = "GH_Circle";
                returnObject.value = getCircle(myCircle.Value);
            }

            GH_Line myLine = obj as GH_Line;
            if (myLine != null)
            {
                returnObject.type = "GH_Line";

                dynamic line = new System.Dynamic.ExpandoObject();
                line.start = myLine.Value.From;
                line.end = myLine.Value.To;

                returnObject.value = line as dynamic;
            }

            GH_Rectangle yourRect = obj as GH_Rectangle;
            if (yourRect != null)
            {
                returnObject.type = "GH_Rectangle";
                dynamic exportRect = new System.Dynamic.ExpandoObject();
                exportRect.width = yourRect.Value.Width;
                exportRect.height = yourRect.Value.Height;
                exportRect.plane = getPlane(yourRect.Value.Plane);
                returnObject.value = exportRect;
            }

            GH_Box myBox = obj as GH_Box;
            if (myBox != null)
            {
                returnObject.type = "GH_Box";
                dynamic exportBox = new System.Dynamic.ExpandoObject();
                exportBox.plane = getPlane(myBox.Value.Plane);
                exportBox.X = getInterval(myBox.Value.X); // interval
                exportBox.Y = getInterval(myBox.Value.Y); // interval
                exportBox.Z = getInterval(myBox.Value.Z); // interval
                returnObject.value = exportBox;
            }

            GH_Surface mySurface = obj as GH_Surface;
            if (mySurface != null)
            {
                returnObject.type = "GH_Surface";
                returnObject.value = ObjectToString(mySurface.Value);

                Mesh[] meshes;
                Mesh joinedMesh = new Mesh();
                meshes = Mesh.CreateFromBrep(mySurface.Value);
                foreach (Mesh tmesh in meshes)
                {
                    joinedMesh.Append(tmesh);
                }

                returnObject.displayValue = new ExpandoObject();
                returnObject.displayValue.faces = joinedMesh.Faces;
                returnObject.displayValue.vertices = joinedMesh.Vertices;
                returnObject.displayValue.vertexColors = joinedMesh.VertexColors;
            }

            GH_Brep myBrep = obj as GH_Brep;
            if (myBrep != null)
            {
                returnObject.type = "GH_Brep";
                returnObject.value = ObjectToString(myBrep.Value);

                Mesh[] meshes;
                Mesh joinedMesh = new Mesh();
                meshes = Mesh.CreateFromBrep(myBrep.Value);
                foreach (Mesh tmesh in meshes)
                {
                    joinedMesh.Append(tmesh);
                }

                returnObject.displayValue = new ExpandoObject();
                returnObject.displayValue.faces = joinedMesh.Faces;
                returnObject.displayValue.vertices = joinedMesh.Vertices;
                returnObject.displayValue.vertexColors = joinedMesh.VertexColors;
            }

            GH_Mesh myMesh = obj as GH_Mesh;
            if (myMesh != null)
            {
                returnObject.type = "GH_Mesh";
                dynamic meshObj = new System.Dynamic.ExpandoObject();
                meshObj.faces = myMesh.Value.Faces;
                meshObj.vertices = myMesh.Value.Vertices;
                meshObj.vertexColors = myMesh.Value.VertexColors;
                returnObject.value = meshObj;
            }

            GH_MeshFace myMesFace = obj as GH_MeshFace;
            if (myMesFace != null)
            {
                returnObject.type = "GH_MeshFace";
                returnObject.value = ObjectToString(myMesFace.Value);
            }

            GH_Boolean myBool = obj as GH_Boolean;
            if (myBool != null)
            {
                returnObject.type = "GH_Boolean";
                returnObject.value = myBool.Value;
            }

            GH_Number myNumber = obj as GH_Number;
            if (myNumber != null)
            {
                returnObject.type = "GH_Number";
                returnObject.value = myNumber.Value;
            }
            GH_Interval myInterval = obj as GH_Interval;
            if (myInterval != null)
            {
                returnObject.type = "GH_Interval";
                returnObject.value = getInterval(myInterval.Value);
            }

            GH_Interval2D myInterval2d = obj as GH_Interval2D;
            if (myInterval2d != null)
            {
                returnObject.type = "GH_Interval2D";
                dynamic exportInterval = new System.Dynamic.ExpandoObject();
                exportInterval.U = getInterval(myInterval2d.Value.U);
                exportInterval.V = getInterval(myInterval2d.Value.V);
                returnObject.value = exportInterval;
            }

            GH_String myString = obj as GH_String;
            if (myString != null)
            {
                returnObject.type = "GH_String";
                returnObject.value = myString.Value;
            }

            GH_Colour myColor = obj as GH_Colour;
            if (myColor != null)
            {
                returnObject.type = "GH_Colour";
                returnObject.value = myColor.Value.ToArgb();
            }

            return returnObject as dynamic;
        }

        public static object castToGH(string type, dynamic value)
        {
            //point, vector, plane, curve, arc, circle, line, rectangle, box, surface, brep, mesh, boolean, number, interval, interval2d, string, color
            object myobj = null;
            switch (type)
            {
                case "GH_Point":
                    myobj = new GH_Point(pointFromDynamic(value));
                    break;
                case "GH_Vector":
                    myobj = new GH_Vector(vectorFromDynamic(value));
                    break;
                case "GH_Plane":
                    myobj = new GH_Plane(planeFromDynamic(value));
                    break;
                case "GH_Polyline":
                case "GH_Curve":
                    myobj = new GH_Curve((Curve)StringToObject((string)value));
                    break;
                case "GH_Arc":
                    myobj = new GH_Arc((Arc)StringToObject((string)value));
                    break;
                case "GH_Circle":
                    myobj = new GH_Circle((Circle)circleFromDynamic(value));
                    break;
                case "GH_Line":
                    myobj = new GH_Line(new Line(pointFromDynamic(value.start), pointFromDynamic(value.end)));
                    break;
                case "GH_Rectangle":
                    myobj = new GH_Rectangle(rectangleFromDynamic(value));
                    break;
                case "GH_Box":
                    myobj = new GH_Box(boxFromDynamic(value));
                    break;
                case "GH_Surface":
                    myobj = new GH_Surface((Brep)StringToObject((string)value));
                    break;
                case "GH_Brep":
                    myobj = new GH_Brep((Brep)StringToObject((string)value));
                    break;
                case "GH_Mesh":
                    myobj = new GH_Mesh(meshFromDynamic(value));
                    break;
                case "GH_Boolean":
                    myobj = new GH_Boolean(Convert.ToBoolean(value));
                    break;
                case "GH_Number":
                    myobj = new GH_Number(Convert.ToDouble(value));
                    break;
                // interval, interval2d, string, color
                case "GH_Interval":
                    myobj = new GH_Interval(intervalFromDynamic(value));
                    break;
                case "GH_Interval2D":
                    myobj = new GH_Interval2D(new UVInterval(intervalFromDynamic(value.U), intervalFromDynamic(value.V)));
                    break;
                case "GH_String":
                    myobj = value;
                    break;
                case "GH_Colour":
                    myobj = new GH_Colour(System.Drawing.Color.FromArgb(Convert.ToInt32(value)));
                    break;
            }
            if (myobj == null) myobj = value;
            return myobj;
        }

        #region Converter Functions: RHINO OBJS > DYNAMIC
        /**

            These function "orchestrate" some conversion rules. Why? 
            We could serialize directly the RhinoCommon object, but 
            some contain too much superflous information; and when 
            you're planning to send millionns of 'em back and forth
            every byte counts. 
            X

        */

        public static dynamic getInterval(Interval myInterval)
        {
            dynamic exportInterval = new System.Dynamic.ExpandoObject();
            exportInterval.min = myInterval.Min;
            exportInterval.max = myInterval.Max;
            return exportInterval;
        }

        public static dynamic getPlane(Plane myPlane)
        {
            dynamic exportPlane = new System.Dynamic.ExpandoObject();
            exportPlane.origin = myPlane.Origin;
            exportPlane.xdir = myPlane.XAxis;
            exportPlane.ydir = myPlane.YAxis;

            return exportPlane;
        }

        public static dynamic getPolyline(PolylineCurve myPoly)
        {
            Polyline p;
            dynamic exportPolyline = new ExpandoObject();
            if (myPoly.TryGetPolyline(out p))
            {
                exportPolyline = p;
                return exportPolyline;
            }
            //myPoly
            return null;
        }

        public static dynamic getCircle(Circle myCircle)
        {
            dynamic exportCircle = new System.Dynamic.ExpandoObject();
            exportCircle.center = myCircle.Center;
            exportCircle.radius = myCircle.Radius;
            exportCircle.plane = getPlane(myCircle.Plane);
            return exportCircle;
        }

        #endregion

        #region Converter functions: STRING > DYNAMIC > RHINO

        /**
            
            These ones here convert back from dynamic objects to Rhino objects. 
            Don't add stuff that converts straight to GH objects. Let's be future proof! 

        */

        public static Point3d pointFromDynamic(dynamic obj)
        {
            return new Point3d((double)obj.X, (double)obj.Y, (double)obj.Z);
        }

        public static Vector3d vectorFromDynamic(dynamic obj)
        {
            return new Vector3d((double)obj.X, (double)obj.Y, (double)obj.Z);
        }

        public static Plane planeFromDynamic(dynamic obj)
        {
            Point3d origin = new Point3d((double)obj.origin.X, (double)obj.origin.Y, (double)obj.origin.Z);
            Vector3d ydir = new Vector3d((double)obj.ydir.X, (double)obj.ydir.Y, (double)obj.ydir.Z);
            Vector3d xdir = new Vector3d((double)obj.xdir.X, (double)obj.xdir.Y, (double)obj.xdir.Z);
            return new Plane(origin, xdir, ydir);
        }

        public static Circle circleFromDynamic(dynamic obj)
        {
            return new Circle(planeFromDynamic(obj.plane), (double)obj.radius);
        }

        public static Interval intervalFromDynamic(dynamic obj)
        {
            return new Interval((double)obj.min, (double)obj.max);
        }

        public static Rectangle3d rectangleFromDynamic(dynamic obj)
        {
            return new Rectangle3d(planeFromDynamic(obj.plane), (double)obj.width, (double)obj.height);
        }

        public static Box boxFromDynamic(dynamic obj)
        {
            return new Box(planeFromDynamic(obj.plane), intervalFromDynamic(obj.X), intervalFromDynamic(obj.Y), intervalFromDynamic(obj.Z));
        }

        public static Mesh meshFromDynamic(dynamic value)
        {
            dynamic obj = value;
            Mesh mesh = new Mesh();
            foreach (var point in obj.vertices)
                mesh.Vertices.Add(new Point3d((double)point.X, (double)point.Y, (double)point.Z));

            foreach (string colour in obj.vertexColors) // uk vs us english is killing me
            {

                string proper = System.Text.RegularExpressions.Regex.Replace(colour, @"\s+", "");
                string[] color = proper.Split(',');
                mesh.VertexColors.Add(Convert.ToInt16(color[0]), Convert.ToInt16(color[1]), Convert.ToInt16(color[2]));
            }

            foreach (var face in obj.faces)
            {
                //NOTE: casting a dynamic prop to bool (ie face.isTriangle) fucks things up big time.
                //don't do it. just leave this shit as it is. don't touch it. 
                bool isTriangle = (int)face.C == (int)face.D ? true : false;
                if (isTriangle)
                    mesh.Faces.AddFace((int)face.A, (int)face.B, (int)face.C);
                else
                    mesh.Faces.AddFace((int)face.A, (int)face.B, (int)face.C, (int)face.D);
            }
            mesh.Normals.ComputeNormals();
            return mesh;
        }

        #endregion

        #region Encoding
        public static string ObjectToString(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(ms, obj);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static object StringToObject(string base64String)
        {
            if (base64String == null) return null;
            byte[] bytes = Convert.FromBase64String(base64String);
            using (MemoryStream ms = new MemoryStream(bytes, 0, bytes.Length))
            {
                ms.Write(bytes, 0, bytes.Length);
                ms.Position = 0;
                return new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(ms);
            }
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        #endregion
    }
}
