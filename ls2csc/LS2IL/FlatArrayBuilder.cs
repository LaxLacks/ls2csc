using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{
    class FlatArrayBuilder
    {
        public FlatArrayBuilder()
        {
            Values = new List<FlatValue>();
        }

        public FlatValue GetFlatValue()
        {
            return new FlatValue(FlatValueType.VT_Array, this.ToString(), this);
        }

        public void Add(FlatValue val)
        {
            Values.Add(val);
        }

        public void FlattenLabels(Function function)
        {
            for (int i = 0; i < Values.Count ; i++)
            {
                FlatValue fv = Values[i];

                if (fv.ValueType == FlatValueType.VT_Label)
                {
                    // get label target
                    int labelValue;
                    if (!function.EmittedLabels.TryGetValue(fv.ValueText, out labelValue))
                    {
                        throw new NotImplementedException("Unresolved label " + fv.ValueText);
                    }
                    
                    Values[i] = FlatValue.Int32(labelValue);
                }
                else if (fv.Object is FlatArrayBuilder)
                {
                    FlatArrayBuilder fab = (FlatArrayBuilder)fv.Object;
                    fab.FlattenLabels(function);
                }
                else if (fv.Object is FlatTableBuilder)
                {
                    FlatTableBuilder fab = (FlatTableBuilder)fv.Object;
                    fab.FlattenLabels(function);
                }
            }

        }

        public List<FlatValue> Values { get; private set; }

        public override string ToString()
        {
            string txt = "{ ";

            bool first = true;
            foreach (FlatValue v in Values)
            {
                if (!first)
                {
                    txt += ", ";
                }
                txt += v.ToString();
                first = false;
            }

            txt += " }";

            return txt;
        }
    }

    class FlatTableBuilder
    {
        public FlatTableBuilder()
        {
            Values = new Dictionary<string, FlatValue>();
        }

        public FlatValue GetFlatValue()
        {

            return new FlatValue(FlatValueType.VT_Table, this.ToString(), this);
        }

        public void Add(string key, FlatValue val)
        {
            Values.Add(key, val);
        }

        public override string ToString()
        {
            // [ "x"=0, "y"=1 ]
            string txt = "[ ";

            bool first = true;
            foreach (KeyValuePair<string, FlatValue> kvp in Values)
            {
                if (!first)
                {
                    txt += ", ";
                }
                txt += "\"" + kvp.Key.ToString().Replace("\"", "\\\"") + "\"=" + kvp.Value.ToString();
                first = false;
            }

            txt += " ]";
            return txt;
        }

        public void FlattenLabels(Function function)
        {
            foreach (KeyValuePair<string, FlatValue> kvp in Values)
            {
                FlatValue fv = kvp.Value;

                if (fv.ValueType == FlatValueType.VT_Label)
                {
                    // get label target
                    int labelValue;
                    if (!function.EmittedLabels.TryGetValue(fv.ValueText, out labelValue))
                    {
                        throw new NotImplementedException("Unresolved label " + fv.ValueText);
                    }

                    fv.ValueType = FlatValueType.VT_Int32;
                    fv.ValueText = labelValue.ToString();
                    fv.Object = labelValue;                   
                }
                else if (fv.Object is FlatArrayBuilder)
                {
                    FlatArrayBuilder fab = (FlatArrayBuilder)fv.Object;
                    fab.FlattenLabels(function);
                }
                else if (fv.Object is FlatTableBuilder)
                {
                    FlatTableBuilder ftb = (FlatTableBuilder)fv.Object;
                    ftb.FlattenLabels(function);
                }
            }
        }

        public Dictionary<string, FlatValue> Values { get; private set; }
    }
}
