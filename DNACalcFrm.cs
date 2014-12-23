using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DNA_Calculator
{
    public partial class DNACalcFrm : Form
    {

        Dictionary<string, string[]> rsid = new Dictionary<string, string[]>();
        long total_bp = 0;
        string filename1 = null;
        string filename2 = null;
        long total_base_pairs = 0;
        bool m_match_no_call = true;
        long m_AllowedErrors=5;
        long m_BasePairs = 5000000;
        long m_GapToBreak=100000;
        long m_SNPs=500;

        long m_match_error_count=0;
        public DNACalcFrm()
        {
            InitializeComponent();
        }

        private void DNACalcFrm_Load(object sender, EventArgs e)
        {
            
        }

        public byte[] Zip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    //msi.CopyTo(gs);
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        public byte[] Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }

                return mso.ToArray();
            }
        }

        public void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                filename2 = dialog.FileName;
                total_bp = 0;
                neanPercent.Text = "0.00%";
                neanPercent.ForeColor = Color.Gray;
                statusLbl.Text = "Calculating ...";
                button1.Enabled = false;
                backgroundWorker2.RunWorkerAsync();
            }
        }

        private string getAutosomalText(string file)
        {
            string text = null;

            if (file.EndsWith(".gz"))
            {
                StringReader reader = new StringReader(Encoding.UTF8.GetString(Unzip(File.ReadAllBytes(file))));
                text = reader.ReadToEnd();
                reader.Close();

            }
            else if (file.EndsWith(".zip"))
            {
                using (var fs = new MemoryStream(File.ReadAllBytes(file)))
                using (var zf = new ZipFile(fs))
                {
                    var ze = zf[0];
                    if (ze == null)
                    {
                        throw new ArgumentException("file not found in Zip");
                    }
                    using (var s = zf.GetInputStream(ze))
                    {
                        using (StreamReader sr = new StreamReader(s))
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
            }
            else
                text = File.ReadAllText(file);
            return text;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.y-str.org/2014/12/dna-calculator.html");
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            StringReader reader = new StringReader(getAutosomalText(filename1));
            string line = null;
            string[] data = null;
            long pos_start = 0;
            long pos_end = 0;
            string chr = null;
            string pchr = null;
            int i_chr = -1;
            total_base_pairs = 0;
            while((line=reader.ReadLine())!=null)
            {
                if (line.StartsWith("#") || line.StartsWith("RSID") || line.Trim().StartsWith("rsid"))
                    continue;
                line = line.Replace("\"", "").Replace("\t", ",");
                data = line.Split(new char[] { ',' });
                if (data.Length == 5)
                    data[3] = data[3] + data[4];

                chr = data[1];

                if (!int.TryParse(chr, out i_chr))
                    continue;
                if (i_chr > 22 || i_chr <= 0)
                    continue;

                if (!rsid.ContainsKey(data[0]))
                    rsid.Add(data[0], data);

                if (chr != pchr || long.Parse(data[2]) - pos_end >= m_GapToBreak)
                {

                    total_base_pairs = total_base_pairs + (pos_end - pos_start);
                    pos_start = long.Parse(data[2]);
                }
                pos_end = long.Parse(data[2]);
                pchr = chr;
            }
            reader.Close();
            //
            if(pchr==chr)
                total_base_pairs = total_base_pairs + (pos_end - pos_start);            
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button1.Enabled = true;
            statusLbl.Text = "Please select second file ...";
        }

        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            StringReader reader = new StringReader(getAutosomalText(filename2));
            string line = null;
            string[] data = null;            
            long segment_start = 0;
            long segment_end = 0;
            string chr=null;
            string pchr=null;
            int i_chr=-1;
            int snp_count = 0;
            bool doub = true;
            m_match_error_count = 0;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#") || line.StartsWith("RSID"))
                    continue;
                line = line.Replace("\"", "").Replace("\t", ",");
                data = line.Split(new char[] { ',' });
                if (data.Length == 5)
                    data[3] = data[3] + data[4];
                if(rsid.ContainsKey(data[0]))
                {
                    chr=data[1];

                    if (!isDoubleMatch(rsid[data[0]][3], data[3]))
                        doub = false;

                    if (!int.TryParse(chr, out i_chr))
                        continue;
                    if (i_chr > 22 || i_chr <= 0)
                        continue;      
                    else if (!isMatch(rsid[data[0]][3], data[3]) || chr != pchr)
                    {
                        if (segment_end - segment_start >= m_BasePairs && snp_count > m_SNPs) // 100000 bp
                        {
                            if (doub)
                                total_bp = total_bp + (segment_end - segment_start)*2;
                            else
                                total_bp = total_bp + (segment_end - segment_start);                            
                        }
                        doub = true;
                        segment_start = long.Parse(rsid[data[0]][2]);
                        snp_count = 0;
                        m_match_error_count = 0;
                    }
                    segment_end = long.Parse(rsid[data[0]][2]);
                    pchr=chr;
                    snp_count++;
                }
            }
            reader.Close();
        }

        public static string ReverseString(string s)
        {
            char[] arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        private bool isDoubleMatch(string p1, string p2)
        {
            if (p1 == p2 || p1==ReverseString(p2))
                 return true;
            return false;
        }

        private bool isMatch(string p1, string p2)
        {
            foreach (char c1 in p1.ToCharArray())
                foreach (char c2 in p2.ToCharArray())
                    if (c1 == c2)
                        return true;
                    else if (m_match_no_call && (c1 == '-' || c2 == '-' || c1 == '?' || c2 == '?' || c1 == '0' || c2 == '0'))
                        return true;

            m_match_error_count++;
            if (m_match_error_count <= m_AllowedErrors)
                return true;
            return false;
        }

        private void backgroundWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

            // because total_bp is HIR, we need to calcualate percentage for each allele.
            double percent = total_bp * 100.0 / (total_base_pairs * 2);// excluding X chromosome and positions not tested by DNA companies and mtdna otherwise it is 3.2 billion bp
            if (percent > 100)
                percent = 100;
            neanPercent.Text = percent.ToString("#0.00") + "%";
            neanPercent.ForeColor = Color.Black;
            statusLbl.Text = "";
            //button1.Enabled = true;
            //
            tbAllowedErrors.Enabled = true;
            tbBasePairs.Enabled = true;
            tbGapToBreak.Enabled = true;
            tbSNPs.Enabled = true;
            cbMatchNoCalls.Enabled = true;
            button3.Enabled = true;
            button4.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                filename1 = dialog.FileName;
                total_bp = 0;
                rsid.Clear();
                neanPercent.Text = "0.00%";
                neanPercent.ForeColor = Color.Gray;
                statusLbl.Text = "Loading ...";
                button1.Enabled = false;

                tbAllowedErrors.Enabled = false;
                tbBasePairs.Enabled = false;
                tbGapToBreak.Enabled = false;
                tbSNPs.Enabled = false;
                cbMatchNoCalls.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;

                long.TryParse(tbAllowedErrors.Text, out m_AllowedErrors);
                long.TryParse(tbBasePairs.Text, out m_BasePairs);
                long.TryParse(tbGapToBreak.Text, out m_GapToBreak);
                long.TryParse(tbSNPs.Text, out  m_SNPs);
                m_match_no_call = cbMatchNoCalls.Checked;
                backgroundWorker1.RunWorkerAsync();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            tbAllowedErrors.Text = "1";
            tbBasePairs.Text = "100000";
            tbGapToBreak.Text = "100000";
            tbSNPs.Text = "60";
            cbMatchNoCalls.Checked = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            tbAllowedErrors.Text = "5";
            tbBasePairs.Text = "5000000";
            tbGapToBreak.Text = "100000";
            tbSNPs.Text = "500";
            cbMatchNoCalls.Checked = true;
        }
    }
}
