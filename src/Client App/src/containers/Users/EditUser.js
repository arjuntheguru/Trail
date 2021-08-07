import React from 'react';
import { Modal } from 'react-bootstrap';
import UserForm from './UserForm';

const EditUser = ({ show, onClose, siteInfo = {}, data }) => {
    
    return (
        <Modal show={show} onHide={onClose} centered>          
            {<UserForm isEdit={true} onClose={onClose} siteInfo={siteInfo} data={data}/>}
        </Modal>
    )
};

export default EditUser;