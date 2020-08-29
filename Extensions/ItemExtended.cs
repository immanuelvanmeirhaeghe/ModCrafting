using Enums;

namespace ModCrafting
{
    class ItemExtended : Item
    {
        public override bool IsImmutableSceneObject()
        {
            if (!IsSceneObject())
            {
                return false;
            }
            if (m_Info != null && m_Info.m_CanBeDamaged)
            {
                return false;
            }
            if (GetInfoID() == ItemID.car_sofa || GetInfoID() == ItemID.mattress_a || GetInfoID() == ItemID.military_bed_toUse
                || GetInfoID() == ItemID.ayuhasca_rack
                || GetInfoID() == ItemID.Ayuhasca_Cauldron_Dream01_Rack_New || GetInfoID() == ItemID.Ayuhasca_Cauldron_Dream02_Rack_New
                || GetInfoID() == ItemID.Ayuhasca_Cauldron_Dream03_Rack_New || GetInfoID() == ItemID.Ayuhasca_Cauldron_Dream04_Rack_New)
            {
                return true;
            }
            return false;
        }
    }
}
