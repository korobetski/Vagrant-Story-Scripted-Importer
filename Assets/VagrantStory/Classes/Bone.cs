using System;

namespace VagrantStory.Classes
{
    [Serializable]
    public class Bone
    {
        public sbyte index;
        //public string name;

        // 12 bytes per bone
        public int length;
        public sbyte parentBoneId;
        public sbyte groupId;
        public byte mountId;
        public byte bodyPartId;
        public sbyte mode;
        public byte[] unk; // 7 null bytes

        private Bone _parentBone;

        public void SetParentBone(Bone bone)
        {
            _parentBone = bone;
        }
        public Bone GetParentBone()
        {
            return _parentBone;
        }

        public int GetDec()
        {
            int dec = 0;
            if (parentBoneId != -1 && parentBoneId != 47)
            {
                dec = _parentBone.length + _parentBone.GetDec();
            }
            return dec;
        }
    }

}
