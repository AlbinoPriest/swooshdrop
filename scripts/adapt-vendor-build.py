from pathlib import Path

path = Path('/opt/airdrop-lab/rtl8188eus/Makefile')
content = path.read_text()
marker = 'ifneq ($(KERNELRELEASE),)'
assert marker in content
if 'ccflags-y += $(EXTRA_CFLAGS)' not in content:
    content = content.replace(marker, '# Kernel 6.18 uses ccflags-y for module compile options.\nccflags-y += $(EXTRA_CFLAGS)\n\n' + marker, 1)
    path.write_text(content)

path = Path('/opt/airdrop-lab/rtl8188eus/os_dep/linux/ioctl_cfg80211.c')
content = path.read_text()
updates = {
    'static int cfg80211_rtw_set_wiphy_params(struct wiphy *wiphy, u32 changed)':
        'static int cfg80211_rtw_set_wiphy_params(struct wiphy *wiphy,\n#if LINUX_VERSION_CODE >= KERNEL_VERSION(6, 18, 0)\n\tint radio_idx,\n#endif\n\tu32 changed)',
    'static int cfg80211_rtw_set_txpower(struct wiphy *wiphy,\n#if (LINUX_VERSION_CODE >= KERNEL_VERSION(3, 8, 0))\n\tstruct wireless_dev *wdev,\n#endif':
        'static int cfg80211_rtw_set_txpower(struct wiphy *wiphy,\n#if (LINUX_VERSION_CODE >= KERNEL_VERSION(3, 8, 0))\n\tstruct wireless_dev *wdev,\n#endif\n#if LINUX_VERSION_CODE >= KERNEL_VERSION(6, 18, 0)\n\tint radio_idx,\n#endif',
    'static int cfg80211_rtw_get_txpower(struct wiphy *wiphy,\n#if (LINUX_VERSION_CODE >= KERNEL_VERSION(3, 8, 0))\n\tstruct wireless_dev *wdev,\n#endif':
        'static int cfg80211_rtw_get_txpower(struct wiphy *wiphy,\n#if (LINUX_VERSION_CODE >= KERNEL_VERSION(3, 8, 0))\n\tstruct wireless_dev *wdev,\n#endif\n#if LINUX_VERSION_CODE >= KERNEL_VERSION(6, 18, 0)\n\tint radio_idx, unsigned int link_id,\n#endif',
    'static int cfg80211_rtw_set_monitor_channel(struct wiphy *wiphy\n#if (LINUX_VERSION_CODE >= KERNEL_VERSION(3, 8, 0))':
        'static int cfg80211_rtw_set_monitor_channel(struct wiphy *wiphy\n#if LINUX_VERSION_CODE >= KERNEL_VERSION(6, 18, 0)\n\t, struct net_device *dev\n#endif\n#if (LINUX_VERSION_CODE >= KERNEL_VERSION(3, 8, 0))',
}
if 'int radio_idx' not in content:
    for old, new in updates.items():
        assert old in content, old
        content = content.replace(old, new, 1)
    path.write_text(content)

path = Path('/opt/airdrop-lab/rtl8188eus/include/osdep_service_linux.h')
content = path.read_text()
compat = '''
#if LINUX_VERSION_CODE >= KERNEL_VERSION(6, 18, 0)
#include <linux/timer.h>
#define from_timer timer_container_of
#define del_timer_sync timer_delete_sync
#define del_timer timer_delete
#endif
'''
if '#define from_timer timer_container_of' not in content:
    content = content.replace('#include <linux/version.h>', '#include <linux/version.h>\n' + compat, 1)
    path.write_text(content)
